$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$project = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$output = Join-Path $project 'Assets/Game/Environment/WallDecals'
$scriptMeta = Join-Path $project 'Packages/com.manta.house-toolkit/Runtime/WallDecalAtlas.cs.meta'
$scriptGuid = [regex]::Match((Get-Content -Raw $scriptMeta), 'guid: ([a-f0-9]{32})').Groups[1].Value
if (!$scriptGuid) { throw "Missing WallDecalAtlas script GUID." }
$padding = 8
$culture = [Globalization.CultureInfo]::InvariantCulture

function Write-AssetText($path, $content) {
    [IO.File]::WriteAllText($path, $content.TrimEnd() + "`n", [Text.UTF8Encoding]::new($false))
}

function Asset-Guid($path, $importer) {
    $meta = "$path.meta"
    if (Test-Path -LiteralPath $meta) {
        $existing = [regex]::Match((Get-Content -Raw -LiteralPath $meta), 'guid: ([a-f0-9]{32})').Groups[1].Value
        if (!$existing) { throw "Missing GUID in $meta." }
        return $existing
    }
    $guid = [Guid]::NewGuid().ToString('N')
    Write-AssetText $meta "fileFormatVersion: 2`nguid: $guid`n$importer"
    return $guid
}

function Number-Text([double] $value) { $value.ToString('0.########', $culture) }

function Build-Atlas($name, $label, $sourceFile, $width, $height, $entries) {
    $source = [Drawing.Bitmap]::new((Join-Path $PSScriptRoot $sourceFile))
    $texture = [Drawing.Bitmap]::new($width, $height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $rows = [Collections.Generic.List[string]]::new()
    $left = 0
    $top = 0
    $rowHeight = 0
    try {
        foreach ($entry in ($entries.Trim() -split '\r?\n')) {
            $id, $title, $themes, $x, $y, $w, $h, $metres = $entry.Split('|')
            $x = [int]$x; $y = [int]$y; $w = [int]$w; $h = [int]$h
            $metres = [double]::Parse($metres, $culture)
            if ($x -lt 0 -or $y -lt 0 -or $w -le 0 -or $h -le 0 -or $x + $w -gt $source.Width -or $y + $h -gt $source.Height) {
                throw "Invalid source rectangle for $id."
            }
            $slotW = $w + 2 * $padding
            $slotH = $h + 2 * $padding
            if ($left + $slotW -gt $width) {
                $left = 0
                $top += $rowHeight
                $rowHeight = 0
            }
            if ($left + $slotW -gt $width -or $top + $slotH -gt $height) { throw "Atlas $name has no space for $id." }
            $destX = $left + $padding
            $destY = $top + $padding
            for ($py = -$padding; $py -lt $h + $padding; $py++) {
                $sy = $y + [Math]::Max(0, [Math]::Min($py, $h - 1))
                for ($px = -$padding; $px -lt $w + $padding; $px++) {
                    $sx = $x + [Math]::Max(0, [Math]::Min($px, $w - 1))
                    $texture.SetPixel($destX + $px, $destY + $py, $source.GetPixel($sx, $sy))
                }
            }
            $u = Number-Text (($destX + .5) / $width)
            $v = Number-Text (($height - $destY - $h + .5) / $height)
            $uw = Number-Text (($w - 1) / $width)
            $vh = Number-Text (($h - 1) / $height)
            $sizeX = Number-Text $metres
            $sizeY = Number-Text ($metres * $h / $w)
            $tags = ($themes.Split(',') | ForEach-Object { "    - $_" }) -join "`n"
            $rows.Add(@"
  - id: $id
    label: $title
    themes:
$tags
    uv: {x: $u, y: $v, width: $uw, height: $vh, serializedVersion: 2}
    size: {x: $sizeX, y: $sizeY}
"@)
            $left += $slotW
            $rowHeight = [Math]::Max($rowHeight, $slotH)
        }
        $png = Join-Path $output "${name}_Atlas.png"
        $texture.Save($png, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $texture.Dispose()
        $source.Dispose()
    }
    $textureGuid = Asset-Guid $png @'
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 1
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 4
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  textureType: 0
  textureShape: 1
  maxTextureSize: 1024
  alphaUsage: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    textureFormat: -1
    textureCompression: 1
'@
    $material = Join-Path $output "${name}_Surface.mat"
    $materialGuid = Asset-Guid $material "NativeFormatImporter:`n  mainObjectFileID: 2100000"
    Write-AssetText $material @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_Name: ${name}_Surface
  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {RenderType: Opaque}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {fileID: 2800000, guid: $textureGuid, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _Cull: 2
    - _Metallic: 0
    - _Smoothness: 0.12
    - _Surface: 0
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}
"@
    $catalog = Join-Path $output "${name}.asset"
    $null = Asset-Guid $catalog "NativeFormatImporter:`n  mainObjectFileID: 11400000"
    Write-AssetText $catalog @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_Script: {fileID: 11500000, guid: $scriptGuid, type: 3}
  m_Name: $label
  m_EditorClassIdentifier:
  material: {fileID: 2100000, guid: $materialGuid, type: 2}
  posters:
$($rows -join "`n")
"@
    Write-Output "$label - $($rows.Count) posters on ${width}x$height"
}

# ID | display name | themes | source bounds (pixels from upper-left) | width in metres
Build-Atlas 'Stackspace' 'Stackspace' 'Stackspace_base_color.png' 512 256 @'
stackspace-header|Stackspace Self Storage|Storage,Advertising|1|1|254|43|2.4
moving-storage|Moving & Storage|Storage,Advertising|1|46|125|49|0.9
units-available|Units Available|Storage,Advertising|129|46|126|49|0.9
need-more-space|Need More Space|Storage,Advertising|1|97|62|59|0.52
packing-supplies|Packing Supplies|Storage,Advertising|65|97|62|59|0.52
short-long-stay|Short & Long Stay|Storage,Advertising|129|97|62|59|0.52
business-storage|Business Storage|Storage,Advertising|193|97|62|59|0.52
reception|Reception|Storage,Wayfinding|1|158|62|29|0.58
loading-bay|Loading Bay|Storage,Wayfinding|65|158|62|29|0.58
unit-block-a|Unit Block A|Storage,Wayfinding|129|158|62|29|0.58
unit-block-b|Unit Block B|Storage,Wayfinding|193|158|62|29|0.58
keep-clear|Keep Clear|Storage,Safety|1|189|62|31|0.5
staff-only|Staff Only|Storage,Safety|65|189|62|31|0.5
cctv-operation|CCTV in Operation|Storage,Safety|129|189|62|31|0.5
way-out|Way Out|Storage,Wayfinding,Safety|193|189|62|31|0.5
unit-a01|Unit A01|Storage,Wayfinding|1|222|62|33|0.32
unit-a02|Unit A02|Storage,Wayfinding|65|222|62|33|0.32
unit-b01|Unit B01|Storage,Wayfinding|129|222|62|33|0.32
unit-b02|Unit B02|Storage,Wayfinding|193|222|62|33|0.32
'@

Build-Atlas 'WorkshopPosters' 'Workshop and Street Posters' 'Posters_base_color.png' 1024 512 @'
open-all-night|Open All Night|Advertising|1|1|63|133|0.36
toolbox|Toolbox|Workshop,Advertising|66|1|130|61|0.8
battery-centre|Battery Centre|Automotive,Advertising|198|1|114|61|0.66
tyre-sale|Tyre Sale|Automotive,Advertising|314|1|110|61|0.62
keys-cut|Keys While You Wait|Workshop,Advertising|426|1|85|61|0.54
thread-repair|Thread & Repair|Workshop,Advertising|66|64|130|70|0.7
unit-24|Unit 24 Storage|Storage,Wayfinding|198|64|88|70|0.48
space-available|Space Available|Storage,Advertising|289|64|130|70|0.72
scrap-wanted|Scrap Wanted|Workshop,Advertising|421|64|90|70|0.52
room-for-more|Room for More Self Storage|Storage,Advertising|1|136|166|149|0.9
parts-dept|Used Car Parts Department|Automotive,Advertising|169|136|161|149|0.9
mend|Mend Workshop & Repairs|Workshop,Advertising|332|136|179|149|1
rustys|Rusty's Motor Repairs|Automotive,Advertising|1|288|183|95|1.05
cubic|Cubic Storage Units|Storage,Advertising|186|288|169|95|0.92
fixing-room|The Fixing Room|Workshop,Advertising|357|288|154|95|0.86
van-rental|Van Rental|Automotive,Advertising|1|386|103|55|0.64
welding|Welding|Workshop,Advertising|106|386|76|55|0.54
paint-shop|Paint Shop|Automotive,Workshop|184|386|69|55|0.45
recovery|Recovery|Automotive,Advertising|255|386|97|55|0.65
coolant-radiators|Coolant & Radiators|Automotive,Advertising|354|386|67|55|0.5
timber-steel|Timber & Steel|Workshop,Advertising|423|386|88|55|0.55
half-full|Half Full Self Storage|Storage,Advertising|1|443|63|68|0.43
circuit|Circuit Electrical Repairs|Workshop,Advertising|66|443|70|68|0.45
bike-doctor|Bike Doctor|Workshop,Advertising|138|443|53|68|0.42
boots-bolts|Boots & Bolts|Workshop,Advertising|193|443|49|68|0.39
load-in|Load In|Storage,Wayfinding|244|443|39|68|0.32
furniture-rescue|Furniture Rescue|Workshop,Advertising|285|443|58|68|0.41
pressure-wash|Pressure Wash|Automotive,Advertising|345|443|50|68|0.41
workshop-to-let|Workshop to Let|Workshop,Advertising|397|443|49|68|0.4
spares-express|Spares Express|Automotive,Advertising|448|443|63|68|0.43
'@
