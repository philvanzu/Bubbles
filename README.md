# Bubbles
Sequential image reader : comics, photo albums library management for Windows with  DX11 graphics, 

Requirements : Windows >= 8.1, GPU

Supported archive formats : zip, rar, cbz, cbr, pdf, plain old file system folder with ".book" extension. They will only show in the library view if they contain at least one image file.

Supported Image formats : jpg, png, bmp, gif, tif... can support all image types for which the Windows Imaging Component Library has a reading codec, just add the extension to BblLibraryNode.imageFileExtensions and recompile.

Highlights: DX11 graphics, smooth zoom with mmb, smooth pan with lmb, rectangle zoom with rmb. scroll and turn pages with the mouse wheel.
Highly configurable image viewer with presets for comics and photo albums.
MultiTabs, each tab has its own settings and is persistent.
List of keyboard shortcuts in the menu.
4 main panels : File System explorer / Library View / Book View / Page View. The file system structures the library, no database.

Quirks : Image folders need to have the ".book" extension to be visible in the library. There's a tool to automatically discover and rename them.

Current Version : Still under active development, there's no version number yet but it is very useable.
Installer Download: https://github.com/philvanzu/Bubbles/raw/master/Bubbles3Setup/Release/
