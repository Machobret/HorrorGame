param([string]$SourceRoot = 'C:\Users\manta\Downloads\IndustrialWallTextures')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class WallTextureResize {
    public static void Process(string source, string destination) {
        const int size = 256;
        using (var input = new Bitmap(source))
        using (var resized = new Bitmap(size, size, PixelFormat.Format32bppArgb)) {
            using (var g = Graphics.FromImage(resized))
            using (var attributes = new ImageAttributes()) {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(input, new Rectangle(0,0,size,size), 0,0,input.Width,input.Height,GraphicsUnit.Pixel,attributes);
            }
            var rectangle = new Rectangle(0,0,size,size);
            var data = resized.LockBits(rectangle,ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            try {
                int stride=data.Stride;
                var pixels=new byte[stride*size];
                Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                var result=(byte[])pixels.Clone();
                // Gaussian blur sigma 0.5 px, then thresholded 45% unsharp mask.
                double[] kernel={Math.Exp(-2),1,Math.Exp(-2)};
                double divisor=Math.Pow(kernel[0]+kernel[1]+kernel[2],2);
                for(int y=0;y<size;y++) for(int x=0;x<size;x++) for(int c=0;c<3;c++) {
                    double blurred=0;
                    for(int ky=-1;ky<=1;ky++) for(int kx=-1;kx<=1;kx++) {
                        int sy=Math.Max(0,Math.Min(size-1,y+ky));
                        int sx=Math.Max(0,Math.Min(size-1,x+kx));
                        blurred+=pixels[sy*stride+sx*4+c]*kernel[ky+1]*kernel[kx+1];
                    }
                    int index=y*stride+x*4+c;
                    double difference=pixels[index]-blurred/divisor;
                    if(Math.Abs(difference)>=3) result[index]=(byte)Math.Max(0,Math.Min(255,Math.Round(pixels[index]+0.45*difference)));
                }
                Marshal.Copy(result,0,data.Scan0,result.Length);
            } finally { resized.UnlockBits(data); }
            resized.Save(destination,ImageFormat.Png);
        }
    }
}
'@
$groups=@('PaintedConcrete','ConcreteBlock','Plaster','TwoTone','Metal','Ceramic','Worn')
$outputRoot=Join-Path $SourceRoot '256'
$count=0
foreach($group in $groups) {
    $files=@(Get-ChildItem -LiteralPath (Join-Path $SourceRoot $group) -Filter '*.png')
    if($files.Count -ne 3){throw "Expected 3 source images in $group"}
    $destination=Join-Path $outputRoot $group
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach($file in $files) {
        $target=Join-Path $destination $file.Name
        [WallTextureResize]::Process($file.FullName,$target)
        $check=[Drawing.Bitmap]::new($target)
        try { if($check.Width -ne 256 -or $check.Height -ne 256){throw "Invalid output size: $target"} } finally {$check.Dispose()}
        $count++
    }
}
@'
21 resized PNG wall textures. Originals preserved in parent material folders.
Processing: Windows System.Drawing high-quality bicubic reduction to 256x256; Gaussian unsharp mask sigma 0.5 pixels, amount 45%, threshold 3/255, applied after resizing. Alpha preserved. No AI regeneration.
Edge sampling is clamped for sharpening and mirrored for bicubic sampling; processing does not establish seamless tiling.
'@ | Set-Content -LiteralPath (Join-Path $outputRoot 'Processing.txt') -Encoding UTF8
Write-Output "Saved and dimension-verified $count PNG textures at 256x256 in $outputRoot"
