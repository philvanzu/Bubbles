using Bubbles3.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace Bubbles3.Models
{
    public partial class BblBookArchive
    {
        /// Nested class interfacing with the archive file.
        /// SharpCompress does not support usage of cached IArchive object from multiple threads 
        /// This class keeps a single thread running for the duration of an open IArchive object
        /// Decompression queries to this IARchive object are transmitted between threads through the _queries queue 
        /// Decompressed images streams are stored in the _results dictionary; 
        /// The pages list is stored in _pages.
        private class BblArchive : IDisposable
        {
            BblBookArchive _book;
            public bool IsValid { get; private set; }
            bool _open = false;
            IArchive _archive;
            Task _task;
            CancellationTokenSource _cancel = null;

            ConcurrentQueue<BblPage> _queries = new ConcurrentQueue<BblPage>();

            ConcurrentDictionary<BblPage, Object> _results = new ConcurrentDictionary<BblPage, object>();
            ObservableCollection<BblPage> _pages;
            public bool _populated { get; private set; }

            public BblArchive(BblBookArchive book)
            {
                _book = book;
                IsValid = true;
            }

            void StartTask()
            {
                _cancel = new CancellationTokenSource();
                _task = new Task(ProcessQueries, _cancel.Token, TaskCreationOptions.LongRunning);
                
                //_task.ContinueWith((t1) =>
                //{
                //    _task = null;
                //    _cancel.Dispose();
                //    _cancel = null;
                //});
                _task.Start();
            }

            private void OpenArchive()
            {
                try
                {
                    _archive = ArchiveFactory.Open(_book.Path);
                    if (_archive != null) _open = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine(String.Format("Cannot open {0} : {1}", _book.Path, e.Message));
                    _open = false;
                }
            }

            private void CloseArchive()
            {
                if (!_open) return;
                try { _archive.Dispose(); }
                catch (Exception e) { Console.WriteLine(e.Message); }
                finally
                {
                    _archive = null;
                    _open = false;
                }
            }

            /// <summary>
            /// Process Request Queue
            /// if the BblPage in the request queue is null, Outputs a sorted List<BblPages> containing all the image entries 
            /// else outputs a MemoryStream containing the the requested page's data.
            /// Check the Responses Dictionary to get the output.
            /// Stays Open for 1.5 seconds after the last request is processed, closes if no new request by then.
            /// </summary>
            async void ProcessQueries()
            {
                try
                {
                    DateTime lastRequest = DateTime.Now;
                    //Console.WriteLine("Starting archive thread for :" + _book.Name);
                    if (!_open) OpenArchive();
                    if (!_open) throw new OperationCanceledException("ProcessQueue Could not open " + _book.Path);
                    while (!_cancel.IsCancellationRequested)
                    {
                        DateTime loopStart = DateTime.Now;
                        BblPage p;
                        if (_queries.TryDequeue(out p))
                        {
                            // dequeuing a null BblPage means we need to build the book's pages list
                            if (p == null)  Populate();
                            else ExtractPage(p);

                            lastRequest = DateTime.Now;
                        }
                        if (!_book.Open) break;

                        double looptime = (DateTime.Now - loopStart).TotalMilliseconds;
                        double sleeptime = Math.Max((100 - looptime), 0);
                        if (sleeptime > 0) await Task.Delay((int)sleeptime);
                        
                    }
                    //Console.WriteLine("Archive Task finished after " + loops.ToString() + " loops");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    IsValid = false;
                }
                finally
                {
                    if (_open) CloseArchive();
                    _task = null;
                    if(_cancel != null) _cancel.Dispose();
                    _cancel = null;
                }
                //Console.WriteLine("exiting archive thread for :" + _book.Name);
            }

            public void Populate()
            {
                List<BblPage> pages = new List<BblPage>();
                try
                {
                    foreach (var entry in _archive.Entries)
                    {
                        string ext = System.IO.Path.GetExtension(entry.Key).ToLowerInvariant();

                        if (IsImageFileExtension(ext) && !entry.IsDirectory)
                        {
                            BblPage page = new BblPage(_book);
                            page.Filename = System.IO.Path.GetFileName(entry.Key);
                            page.Path = entry.Key;
                            page.Size = entry.Size;
                            page.CreationTime = (entry.CreatedTime.HasValue) ? entry.CreatedTime.Value : _book.CreationTime;
                            page.LastWriteTime = (entry.LastModifiedTime.HasValue) ? entry.LastModifiedTime.Value : _book.LastWriteTime;
                            page.LastAccessTime = _book.LastAccessTime;
                            pages.Add(page);
                        }
                    }
                    if (pages != null)
                    {
                        pages.Sort();
                        _pages = new ObservableCollection<BblPage>(pages);
                        ExtractPage(_pages[0]);
                        _populated = true;
                    }
                    else throw new InvalidOperationException("Could not load pages list from archive file :" + _book.Path);

                }
                catch
                {
                    _pages = null;
                    _populated = false;
                }
                
            }


            public ObservableCollection<BblPage> GetPagesList()
            {
                
                _queries.Enqueue(null);
                if (_task == null) StartTask();
                int count = 0;

                while (_task != null && !_populated)
                {
                    Thread.Sleep(10);
                    if (_task == null) count++;
                    if (count > 20) break;
                }

                if (!_populated)
                {
                    Console.WriteLine("Archive population Error for book :" + _book.Path);
                }

                return _pages;
            }

            public void ExtractPage(BblPage p)
            {
                try
                {
                    //Console.WriteLine(_book.Name + "::" + p.Index.ToString() + " Extracting");
                    var entry = _archive.Entries.Where(x => x.Key == p.Path).FirstOrDefault();
                    MemoryStream stream = new MemoryStream((int)entry.Size);
                    entry.WriteTo(stream);
                    _results.TryAdd(p, stream);
                }
                catch (Exception e) { Console.WriteLine("BblArchive ExtractPage failed : " + e.Message); }
                
            }

            public MemoryStream GetPageData(BblPage page)
            {
                if (page.Index != 0)
                {
                    _queries.Enqueue(page);
                    if (_task == null) StartTask();
                }

                object value = null;
                int count = 0;
                while (!_results.TryRemove(page, out value))
                {
                    Thread.Sleep(10);
                    if (_task == null) count++;
                    if (count > 20) break;
                }

                if(value == null)
                {
                    Console.WriteLine("Get Page Data Error for book :" + _book.Path);
                }
                return value as MemoryStream;
            }

            public void Dispose()
            {
                if (_task != null && _task.Status == TaskStatus.Running) _cancel.Cancel();
                if (_results != null && _results.Count > 0)
                    foreach (var r in _results) (r.Value as MemoryStream).Dispose();
            }

        }

        
    }
    
}
