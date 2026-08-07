# 生成扫雷应用图标（Win11 Fluent 风格：靛蓝→紫渐变圆角方块 + 半透明棋盘格 + 红色地雷 + 旗帜 + 问号）
# 同时生成：MSIX 全套 Assets PNG、exe 图标 AppIcon.ico（多尺寸）
# 用法: powershell -ExecutionPolicy Bypass -File scripts\generate-icons.ps1
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$assetDir = Join-Path $PSScriptRoot "..\src\Minesweeper\Assets"
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null

# ---------- 圆角矩形路径 ----------
function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

# ---------- 在 256 设计坐标系上绘制全部内容（任意尺寸缩放） ----------
function Draw-IconArt([System.Drawing.Graphics]$g, [float]$scale) {
    # 1) 背景：靛蓝 → 紫色 对角渐变圆角方块（Win11 风格）
    $c1 = [System.Drawing.Color]::FromArgb(255, 79, 70, 229)   # #4F46E5 靛蓝
    $c2 = [System.Drawing.Color]::FromArgb(255, 147, 92, 246)  # #935CF6 紫
    $bgRect = [System.Drawing.RectangleF]::new(0, 0, 256 * $scale, 256 * $scale)
    $bgPath = New-RoundedRectPath 0 0 (256 * $scale) (256 * $scale) (52 * $scale)
    $g.Clip = New-Object System.Drawing.Region($bgPath)
    # 对角渐变（线性插值逐行，简单可靠）
    for ($y = 0; $y -lt [int](256 * $scale); $y++) {
        $t1 = $y / (256 * $scale)
        for ($x = 0; $x -lt [int](256 * $scale); $x += 2) {
            $t = (($x / (256 * $scale)) + $t1) / 2
            $cr = [int]($c1.R + ($c2.R - $c1.R) * $t)
            $cg = [int]($c1.G + ($c2.G - $c1.G) * $t)
            $cb = [int]($c1.B + ($c2.B - $c1.B) * $t)
            $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, $cr, $cg, $cb), 2)
            $g.DrawLine($pen, $x, $y, $x + 1, $y)
            $pen.Dispose()
        }
    }

    # 2) 3x3 半透明棋盘格
    $cell = 48 * $scale
    $gap = 8 * $scale
    $gridLeft = 48 * $scale
    $gridTop = 48 * $scale
    $cellBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(120, 255, 255, 255))
    for ($r = 0; $r -lt 3; $r++) {
        for ($c = 0; $c -lt 3; $c++) {
            $x = $gridLeft + $c * ($cell + $gap)
            $y = $gridTop + $r * ($cell + $gap)
            $p = New-RoundedRectPath $x $y $cell $cell (10 * $scale)
            $g.FillPath($cellBrush, $p)
            $p.Dispose()
        }
    }
    $cellBrush.Dispose()

    # 3) 中央格：红色地雷（球体 + 8 刺 + 高光）
    $cx = 128 * $scale; $cy = 128 * $scale
    $r = 24 * $scale
    $spike = 16 * $scale
    $spikePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 122, 22, 38), [float](5.5 * $scale))
    $spikePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $spikePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    for ($i = 0; $i -lt 8; $i++) {
        $ang = [Math]::PI * 2 * $i / 8
        $x1 = [float]($cx + [Math]::Cos($ang) * ($r * 0.72))
        $y1 = [float]($cy + [Math]::Sin($ang) * ($r * 0.72))
        $x2 = [float]($cx + [Math]::Cos($ang) * ($r + $spike))
        $y2 = [float]($cy + [Math]::Sin($ang) * ($r + $spike))
        $g.DrawLine($spikePen, $x1, $y1, $x2, $y2)
    }
    $spikePen.Dispose()
    # 球体（左上亮 → 右下暗 渐变）
    $ballRect = [System.Drawing.RectangleF]::new(
        [float]($cx - $r), [float]($cy - $r), [float]($r * 2), [float]($r * 2))
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $ballRect,
        [System.Drawing.Color]::FromArgb(255, 232, 93, 110),   # #E85D6E
        [System.Drawing.Color]::FromArgb(255, 158, 34, 50),    # #9E2232
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillEllipse($grad, $ballRect)
    $grad.Dispose()
    # 高光
    $hiBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 255, 255, 255))
    $g.FillEllipse($hiBrush, [float]($cx - 13 * $scale), [float]($cy - 17 * $scale),
                   [float](16 * $scale), [float](9 * $scale))
    $hiBrush.Dispose()
    # 球体描边
    $outline = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 90, 10, 26), [float](2.5 * $scale))
    $g.DrawEllipse($outline, $ballRect)
    $outline.Dispose()

    # 4) 左上格：红色小旗
    $fx = 72 * $scale; $fy = 72 * $scale
    $pole = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 31, 41, 55), [float](3.5 * $scale))
    $pole.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pole.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pole, [float]($fx), [float]($fy - 12 * $scale), [float]($fx), [float]($fy + 14 * $scale))
    $pole.Dispose()
    $flagBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 244, 63, 94))
    $flagPts = @(
        [System.Drawing.PointF]::new([float]($fx + 2 * $scale), [float]($fy - 10 * $scale)),
        [System.Drawing.PointF]::new([float]($fx + 18 * $scale), [float]($fy - 4 * $scale)),
        [System.Drawing.PointF]::new([float]($fx + 2 * $scale), [float]($fy + 3 * $scale))
    )
    $g.FillPolygon($flagBrush, $flagPts)
    $flagBrush.Dispose()
    $baseBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 31, 41, 55))
    $g.FillRectangle($baseBrush, [float]($fx - 6 * $scale), [float]($fy + 13 * $scale),
                     [float](12 * $scale), [float](4 * $scale))
    $baseBrush.Dispose()

    # 5) 右下格：白色粗体问号
    $qFont = New-Object System.Drawing.Font("Segoe UI", [float](46 * $scale), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $qBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $qRect = [System.Drawing.RectangleF]::new(
        [float](160 * $scale), [float](160 * $scale), [float](48 * $scale), [float](48 * $scale))
    $qFmt = New-Object System.Drawing.StringFormat
    $qFmt.Alignment = [System.Drawing.StringAlignment]::Center
    $qFmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("?", $qFont, $qBrush, $qRect, $qFmt)
    $qFmt.Dispose(); $qBrush.Dispose(); $qFont.Dispose()

    $g.ResetClip()
    $bgPath.Dispose()
}

# ---------- 单个图标位图（透明背景 + 圆角方块内容） ----------
function New-IconImage([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)
    Draw-IconArt $g ($size / 256.0)
    $g.Dispose()
    return $bmp
}

# ---------- 宽磁贴 310x150（全幅渐变 + 左侧图标 + 右侧标题文字） ----------
function New-WideTile() {
    $bmp = New-Object System.Drawing.Bitmap(310, 150, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $c1 = [System.Drawing.Color]::FromArgb(255, 79, 70, 229)
    $c2 = [System.Drawing.Color]::FromArgb(255, 147, 92, 246)
    for ($y = 0; $y -lt 150; $y++) {
        $t1 = $y / 150.0
        for ($x = 0; $x -lt 310; $x += 2) {
            $t = (($x / 310.0) + $t1) / 2
            $cr = [int]($c1.R + ($c2.R - $c1.R) * $t)
            $cg = [int]($c1.G + ($c2.G - $c1.G) * $t)
            $cb = [int]($c1.B + ($c2.B - $c1.B) * $t)
            $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, $cr, $cg, $cb), 2)
            $g.DrawLine($pen, $x, $y, $x + 1, $y)
            $pen.Dispose()
        }
    }
    # 左侧图标（150x150，左边距 30）
    $g.TranslateTransform(30, 0)
    Draw-IconArt $g (150 / 256.0)
    $g.ResetTransform()
    # 右侧标题
    $font = New-Object System.Drawing.Font("Microsoft YaHei UI", 30, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $rect = [System.Drawing.RectangleF]::new(195, 0, 110, 150)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("扫雷", $font, $brush, $rect, $fmt)
    $fmt.Dispose(); $brush.Dispose(); $font.Dispose()
    $g.Dispose()
    return $bmp
}

# ---------- 启动屏 620x620（居中缩小版图标，透明背景） ----------
function New-SplashImage() {
    $size = 620
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.TranslateTransform(190, 190)   # 居中 240x240
    Draw-IconArt $g (240 / 256.0)
    $g.Dispose()
    return $bmp
}

# ---------- 多尺寸 ICO（PNG 压缩条目，Win11 支持） ----------
function New-IcoFile([string]$path, [int[]]$sizes) {
    $entries = @()
    foreach ($s in $sizes) {
        $bmp = New-IconImage $s
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $entries += , @($s, $ms.ToArray())
        $bmp.Dispose(); $ms.Dispose()
    }
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    foreach ($e in $entries) {
        $s = $e[0]
        $b = if ($s -ge 256) { 0 } else { $s }
        $bw.Write([byte]$b); $bw.Write([byte]$b)
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$e[1].Length); $bw.Write([uint32]$offset)
        $offset += $e[1].Length
    }
    foreach ($e in $entries) { $bw.Write($e[1]) }
    $bw.Close(); $fs.Close()
}

# ---------- 输出 ----------
$squareSizes = @{
    "Square44x44Logo.png"               = 44
    "Square44x44Logo.targetsize-44.png" = 44
    "Square71x71Logo.png"               = 71
    "Square150x150Logo.png"             = 150
    "Square310x310Logo.png"             = 310
    "StoreLogo.png"                     = 50
}
foreach ($entry in $squareSizes.GetEnumerator()) {
    $bmp = New-IconImage $entry.Value
    $bmp.Save((Join-Path $assetDir $entry.Key), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "已生成 $($entry.Key)"
}
$wide = New-WideTile
$wide.Save((Join-Path $assetDir "Wide310x150Logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$wide.Dispose()
Write-Host "已生成 Wide310x150Logo.png"

$splash = New-SplashImage
$splash.Save((Join-Path $assetDir "SplashScreen.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$splash.Dispose()
Write-Host "已生成 SplashScreen.png"

New-IcoFile (Join-Path $assetDir "AppIcon.ico") @(16, 24, 32, 48, 64, 128, 256)
Write-Host "已生成 AppIcon.ico（16~256 多尺寸）"

# 预览图（256px 主图，方便查看）
$preview = New-IconImage 256
$preview.Save((Join-Path (Split-Path $PSScriptRoot -Parent) "docs\icon-preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()
Write-Host "已生成预览 docs\icon-preview.png"
Write-Host "图标生成完成 → $assetDir"
