# Each 256 px bay covers 3 x 2.98 m. Door crops retain the approved atlas scale.
param([string]$OutputPath=(Join-Path $PSScriptRoot '../../../Assets/Game/Environment/FillerBuildings/TownFacades_Atlas.png'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'TownFacades_Source.png'))
$details=[Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'GlazingAndCoping_Source.png'))
$atlas=[Drawing.Bitmap]::new(1536,1536)
$display=[Drawing.Bitmap]::new(768,768)
$graphics=[Drawing.Graphics]::FromImage($atlas)
$shop=[Drawing.Graphics]::FromImage($display)
function Blit($g,$img,$x,$y,$w,$h,$sx,$sy,$sw,$sh) {
    $g.DrawImage($img,[Drawing.Rectangle]::new($x,$y,$w,$h),$sx,$sy,$sw,$sh,[Drawing.GraphicsUnit]::Pixel)
}
try {
    foreach($g in @($graphics,$shop)) { $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic }
    foreach($img in @($source,$details)) { if($img.Width -ne 1254 -or $img.Height -ne 1254){throw 'Unexpected source dimensions.'} }
    # Frame bounds and sill in the packed cell followed by source x and width.
    $frames=@(@(150,122,62,429,150),@(122,109,67,844,123),@(103,83,86,633,166),@(122,109,67,844,123),@(150,122,62,429,150))
    $wy=@(12,23,24,16,16)
    $wh=@(180,168,162,166,159)
    $doorX=@(40,258,441,652,846)
    $doorW=@(137,113,133,132,130)
    # Glass apertures measured inside the frames, preserving every mullion and transom.
    $panes=@(
      @('85,99,40,40','85,143,40,31','138,99,39,40','138,143,39,31'),
      @('89,94,23,19','121,94,23,19','153,94,22,19','89,123,37,47','138,123,35,47'),
      @('88,97,35,63','133,97,35,63'),
      @('80,91,27,22','113,91,29,22','148,91,27,22','80,118,27,26','113,118,29,26','148,118,27,26','80,149,27,25','113,149,29,25','148,149,27,25'),
      @('76,92,44,37','76,135,44,38','139,92,42,37','139,135,42,38'))
    for($row=0;$row -lt 5;$row++){
        $y=$row*256
        $w,$h,$sill,$sx,$sw=$frames[$row]
        for($col=0;$col -lt 6;$col++){
            $x=$col*256
            Blit $graphics $source $x $y 256 256 4 ($row*209+5) 204 198
            if($col -eq 5){
                $dw=[int]($doorW[$row]*181/207)
                Blit $graphics $source ($x+[int]((256-$dw)/2)) ($y+75) $dw 181 $doorX[$row] 1047 $doorW[$row] 207
            }elseif($col -gt 0){
                Blit $graphics $source ($x+[int]((256-$w)/2)) ($y+256-$sill-$h) $w $h $sx ($row*209+$wy[$row]) $sw $wh[$row]
                # Map the treatment continuously behind all panes of this opening.
                foreach($rect in $panes[$row]){
                    $px,$py,$pw,$ph=[int[]]($rect.Split(','))
                    $gx=24+($px-72)*5
                    $gy=($col-1)*251+8+($py-90)*2.6
                    Blit $graphics $details ($x+$px) ($y+$py) $pw $ph $gx $gy ($pw*5) ($ph*2.6)
                }
            }
        }
        # Optional wide shopfronts stay available independently of the six core bays.
        $shopX=($row%3)*256
        $shopY=[math]::Floor($row/3)*256
        Blit $shop $source $shopX $shopY 256 256 4 ($row*209+5) 204 198
        $dw=[int](145*241/$wh[$row])
        Blit $shop $source ($shopX+[int]((256-$dw)/2)) ($shopY+64) $dw 145 1007 ($row*209+$wy[$row]) 241 $wh[$row]
    }
    # Preview each coping over its matching wall. Only the top 40 pixels map to the roofline.
    for($col=0;$col -lt 6;$col++){
        $family=$col%5
        Blit $graphics $source ($col*256) 1280 256 256 4 ($family*209+5) 204 198
        $sourceY=if($col -eq 5){252}else{$family*251+2}
        $sourceH=if($col -eq 5){52}else{244}
        Blit $graphics $details ($col*256) 1280 256 40 630 $sourceY 620 $sourceH
    }
    $full=[IO.Path]::GetFullPath($OutputPath)
    $atlas.Save($full,[Drawing.Imaging.ImageFormat]::Png)
    $display.Save((Join-Path ([IO.Path]::GetDirectoryName($full)) 'TownStorefronts_Atlas.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose(); $shop.Dispose(); $atlas.Dispose(); $display.Dispose(); $source.Dispose(); $details.Dispose()
}
