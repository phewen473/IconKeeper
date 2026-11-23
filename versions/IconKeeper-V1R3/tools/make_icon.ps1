# make_icon.ps1 - draws the IconKeeper checkmark icon and writes src\app.ico (sizes 16-256, PNG entries).  # file purpose
# Run once after changing the design:  powershell -ExecutionPolicy Bypass -File tools\make_icon.ps1           # how to run it
Add-Type -AssemblyName System.Drawing                                                     # .NET drawing library (GDI+)
$out   = Join-Path $PSScriptRoot '..\src\app.ico'                                         # where the finished .ico goes
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256                                             # every size Windows may ask for
$pngs  = @()                                                                              # PNG bytes for each size, filled below

foreach ($s in $sizes) {                                                                  # draw one image per size
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb) # transparent canvas
    $g   = [System.Drawing.Graphics]::FromImage($bmp)                                     # drawing surface for it
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias                # smooth edges
    $g.Clear([System.Drawing.Color]::Transparent)                                         # start fully transparent
    $m = [Math]::Max(1, [Math]::Round($s * 0.04))                                         # small margin around the tile
    $w = $s - 2 * $m                                                                      # tile width/height
    $r = [Math]::Max(2, [Math]::Round($s * 0.22))                                         # corner radius of the tile
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath                              # outline of a rounded square
    $path.AddArc($m, $m, $r, $r, 180, 90)                                                 # top-left corner
    $path.AddArc($m + $w - $r, $m, $r, $r, 270, 90)                                       # top-right corner
    $path.AddArc($m + $w - $r, $m + $w - $r, $r, $r, 0, 90)                               # bottom-right corner
    $path.AddArc($m, $m + $w - $r, $r, $r, 90, 90)                                        # bottom-left corner
    $path.CloseFigure()                                                                   # join it up
    $top    = [System.Drawing.Color]::FromArgb(255, 67, 176, 71)                          # lighter green at the top
    $bottom = [System.Drawing.Color]::FromArgb(255, 30, 125, 50)                          # darker green at the bottom
    $rect   = New-Object System.Drawing.RectangleF 0, 0, $s, $s                           # area the gradient covers
    $fill   = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $top, $bottom, 90.0 # top-to-bottom gradient
    $g.FillPath($fill, $path)                                                             # paint the green tile
    $pw  = [Math]::Max(2.0, $s * 0.14)                                                    # checkmark line thickness
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $pw             # white pen for the check
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round                             # rounded line ends
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round                             # rounded line ends
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round                            # rounded corner at the bottom of the check
    $p1 = New-Object System.Drawing.PointF ($s * 0.25), ($s * 0.52)                       # check: left end
    $p2 = New-Object System.Drawing.PointF ($s * 0.43), ($s * 0.70)                       # check: bottom point
    $p3 = New-Object System.Drawing.PointF ($s * 0.76), ($s * 0.32)                       # check: right end
    $g.DrawLines($pen, [System.Drawing.PointF[]]@($p1, $p2, $p3))                         # draw the checkmark
    $ms = New-Object System.IO.MemoryStream                                               # buffer for this image's bytes
    if ($s -ge 256) {                                                                     # the big size is stored as PNG (smaller file)
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)                         # encode as PNG
    } else {                                                                              # small sizes: classic 32-bit bitmap, read by every loader
        $hw = New-Object System.IO.BinaryWriter $ms                                       # writer into the buffer
        $hw.Write([UInt32]40); $hw.Write([Int32]$s); $hw.Write([Int32]($s * 2))           # BITMAPINFOHEADER: size, width, height (x2 = colour + mask)
        $hw.Write([UInt16]1); $hw.Write([UInt16]32)                                       # 1 plane, 32 bits per pixel
        $hw.Write([UInt32]0); $hw.Write([UInt32]0)                                        # no compression, image size may be 0
        $hw.Write([Int32]0); $hw.Write([Int32]0); $hw.Write([UInt32]0); $hw.Write([UInt32]0) # resolution and palette fields unused
        $rect2 = New-Object System.Drawing.Rectangle 0, 0, $s, $s                         # whole bitmap
        $data = $bmp.LockBits($rect2, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)) # get raw pixels (BGRA)
        $row = New-Object byte[] ($s * 4)                                                 # one row of pixels
        for ($y = $s - 1; $y -ge 0; $y--) {                                               # icon bitmaps are stored bottom row first
            [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $row, 0, $s * 4) # copy row y
            $hw.Write($row)                                                               # write it
        }                                                                                 # end of row loop
        $bmp.UnlockBits($data)                                                            # release the pixels
        $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)                                  # AND-mask row length (padded to 4 bytes)
        $hw.Write((New-Object byte[] ($maskRow * $s)))                                    # all-zero mask: transparency comes from the alpha channel
        $hw.Flush()                                                                       # push everything into the buffer
    }                                                                                     # end of format choice
    $pngs += ,$ms.ToArray()                                                               # keep the bytes (name kept from v1; holds PNG or bitmap)
    $g.Dispose(); $bmp.Dispose(); $pen.Dispose(); $fill.Dispose(); $path.Dispose()        # free drawing objects
}                                                                                         # end of size loop

$fs = [System.IO.File]::Create($out)                                                      # create the .ico file
$bw = New-Object System.IO.BinaryWriter $fs                                               # writer for binary fields
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)               # ICO header: reserved, type 1 = icon, image count
$offset = 6 + 16 * $sizes.Count                                                           # image data starts after header + directory
for ($i = 0; $i -lt $sizes.Count; $i++) {                                                 # one directory entry per image
    $s = $sizes[$i]                                                                       # this image's size
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))                               # width (0 means 256)
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))                               # height (0 means 256)
    $bw.Write([byte]0); $bw.Write([byte]0)                                                # no palette, reserved
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)                                           # 1 colour plane, 32 bits per pixel
    $bw.Write([UInt32]$pngs[$i].Length); $bw.Write([UInt32]$offset)                       # data length and where it starts
    $offset += $pngs[$i].Length                                                           # next image follows this one
}                                                                                         # end of directory loop
foreach ($p in $pngs) { $bw.Write($p) }                                                   # write the PNG data for every size
$bw.Close()                                                                               # finish and close the file
"Wrote $((Resolve-Path $out).Path)"                                                       # report where it went
