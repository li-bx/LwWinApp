<#
.SYNOPSIS
    生成应用图标：品牌色圆角方块 + 白色字母标记，输出多尺寸 .ico。

.DESCRIPTION
    改品牌色或改字母之后重跑这一个脚本就行，不用找设计工具重新导出。
    输出的 .ico 会被 LwWinApp.csproj 的 <ApplicationIcon> 引用，
    成为 exe 的图标资源 —— 任务栏、资源管理器、窗口标题栏看到的都是它。

    每个尺寸都是从 4 倍画布降采样得到的，而不是直接在小画布上画字：
    小画布上渲染 16px 文字会糊成一团，先大后小才能保住字形。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-icon.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-icon.ps1 -Color '#C42B1C' -Text 'AB'
#>
param(
    # 输出目录（相对解决方案根目录）。
    [string]$OutDir = 'LwWinApp\Assets',

    # 方块上的字母标记。
    [string]$Text = 'LW',

    # 底色。默认取 Themes/Colors.xaml 里的 BrandPrimaryColor。
    [string]$Color = '#0F6CBD',

    # 字母颜色。
    [string]$Foreground = '#FFFFFF'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Windows 会按 DPI 和场景挑最合适的尺寸，这一套基本够用。
# 16 = 任务栏 / 列表小图标，32 = 桌面大图标，256 = 特大图标与商店。
$sizes = @(256, 128, 64, 48, 32, 24, 16)

# 先大后小，半径按比例走，任何尺寸下圆角的观感才一致。
$cornerRadiusRatio = 0.23

# 字母整体宽度占画布的比例。太小显不出标记，太大贴边。
# 小尺寸单独给一档：同样的比例在 16px 上笔画太细，降采样之后会糊掉。
$textWidthRatio = 0.60
$textWidthRatioSmall = 0.68

# 小尺寸再额外描一圈同色的边，把笔画加粗。
# 「W」在 16px 下只有约 7 个像素宽却要放下四条斜线，不补这一下认不出是字母。
$strokeWidthRatio = 0.012

function New-RoundedSquarePath([single]$size, [single]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    # 四段圆弧 + 四条边。AddArc 的矩形是外接矩形，所以要按直径算。
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$size, [string]$text, [string]$bg, [string]$fg) {
    $supersample = 4
    $canvas = $size * $supersample

    $big = New-Object System.Drawing.Bitmap $canvas, $canvas
    $g = [System.Drawing.Graphics]::FromImage($big)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $g.Clear([System.Drawing.Color]::Transparent)

        $sizeF = [single]$canvas
        $path = New-RoundedSquarePath $sizeF ([single]($canvas * $cornerRadiusRatio))
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($bg))
        $g.FillPath($brush, $path)
        $brush.Dispose()
        $path.Dispose()

        # GenericTypographic 量出来的框不含边距，用它的高度居中才不偏。
        # 用默认的 StringFormat 会因为预留了行距把字压得偏上。
        $fmt = [System.Drawing.StringFormat]::GenericTypographic

        # 先量一次，再按目标宽度反推字号，这样换字母也不用重调参数。
        $ratio = if ($size -le 32) { $textWidthRatioSmall } else { $textWidthRatio }
        $targetWidth = $canvas * $ratio

        $probe = New-Object System.Drawing.Font 'Segoe UI', ([single]($canvas * 0.5)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
        $measured = $g.MeasureString($text, $probe, [System.Drawing.PointF]::Empty, $fmt)
        $fontSize = [single]($canvas * 0.5 * $targetWidth / $measured.Width)
        $fontFamily = $probe.FontFamily
        $probe.Dispose()

        # 走 GraphicsPath 而不是 DrawString：路径是矢量的，缩放时不会带上
        # GDI+ 在小字号下做的字形微调，降采样之后更干净；而且能顺带描边加粗。
        $glyphs = New-Object System.Drawing.Drawing2D.GraphicsPath
        $glyphs.AddString($text, $fontFamily, [int][System.Drawing.FontStyle]::Bold, $fontSize,
            (New-Object System.Drawing.PointF 0, 0), $fmt)

        # 按路径自身的外框居中。AddString 的原点在基线上，
        # 直接放到画布中心会让字母整体偏上（基线下方的部分被算进去了）。
        $bounds = $glyphs.GetBounds()
        $matrix = New-Object System.Drawing.Drawing2D.Matrix
        $matrix.Translate(
            [single](($canvas - $bounds.Width) / 2 - $bounds.X),
            [single](($canvas - $bounds.Height) / 2 - $bounds.Y))
        $glyphs.Transform($matrix)
        $matrix.Dispose()

        $fgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($fg))
        $g.FillPath($fgBrush, $glyphs)

        if ($size -le 32) {
            # 用同色描边把笔画撑粗一点。圆角连接避免尖角在降采样后炸开。
            $pen = New-Object System.Drawing.Pen $fgBrush, ([single]($canvas * $strokeWidthRatio))
            $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
            $g.DrawPath($pen, $glyphs)
            $pen.Dispose()
        }

        $fgBrush.Dispose()
        $glyphs.Dispose()
        $fontFamily.Dispose()

        # 降采样到目标尺寸。HighQualityBicubic + 高质量像素偏移
        # 是 GDI+ 里缩图最干净的一组参数，少了 PixelOffsetMode 会有半像素偏移。
        $result = New-Object System.Drawing.Bitmap $size, $size
        $g2 = [System.Drawing.Graphics]::FromImage($result)
        try {
            $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $g2.Clear([System.Drawing.Color]::Transparent)
            $g2.DrawImage($big, (New-Object System.Drawing.Rectangle 0, 0, $size, $size))
        }
        finally { $g2.Dispose() }

        return $result
    }
    finally {
        $g.Dispose()
        $big.Dispose()
    }
}

# ---------------------------------------------------------------
# 生成各尺寸 PNG
# ---------------------------------------------------------------

$pngs = @()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size $Text $Color $Foreground
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    if ($size -eq 256) {
        # 顺手留一张 PNG：给别人看、或做 MSIX 磁贴图都方便。
        $bmp.Save((Join-Path $OutDir 'AppIcon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $ms.Dispose()
    $bmp.Dispose()
    Write-Host ("  {0,3}x{0,-3} OK" -f $size)
}

# ---------------------------------------------------------------
# 打包成 .ico
#
# ICO 只是一个容器：6 字节头 + 每张图 16 字节目录项 + 各图数据。
# 这里每张图都存 PNG 原始字节（Vista 以后支持），省得手写 BMP 的
# 调色板与掩码 —— 那是 32bpp 图在 ICO 里最容易出错的地方。
# ---------------------------------------------------------------

$outFile = Join-Path $OutDir 'AppIcon.ico'
$fs = [System.IO.File]::Create($outFile)
$bw = New-Object System.IO.BinaryWriter($fs)
try {
    $bw.Write([UInt16]0)               # 保留位
    $bw.Write([UInt16]1)               # 1 = 图标（2 是光标）
    $bw.Write([UInt16]$pngs.Count)

    $offset = 6 + 16 * $pngs.Count
    for ($i = 0; $i -lt $pngs.Count; $i++) {
        $size = $sizes[$i]
        # 目录项里的宽高各占 1 字节，256 只能写 0（这是格式的规定，不是省略）。
        $bw.Write([byte]$(if ($size -ge 256) { 0 } else { $size }))
        $bw.Write([byte]$(if ($size -ge 256) { 0 } else { $size }))
        $bw.Write([byte]0)             # 调色板色数，真彩色写 0
        $bw.Write([byte]0)             # 保留位
        $bw.Write([UInt16]1)           # 色彩平面
        $bw.Write([UInt16]32)          # 每像素位数
        $bw.Write([UInt32]$pngs[$i].Length)
        $bw.Write([UInt32]$offset)
        $offset += $pngs[$i].Length
    }

    foreach ($png in $pngs) { $bw.Write($png) }
}
finally {
    $bw.Dispose()
    $fs.Dispose()
}

$icoSize = (Get-Item $outFile).Length
Write-Host ""
Write-Host "已生成 $outFile（$($sizes.Count) 个尺寸，$icoSize 字节）"
