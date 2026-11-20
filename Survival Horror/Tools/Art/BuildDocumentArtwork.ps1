$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Push-Location (Join-Path $PSScriptRoot '../..')
try {
    $chapter = 'Assets/Game/Levels/Chapter1/Assets'
    $newspaper = 'Assets/Game/Items/Newspaper'
    $fontFiles = [Drawing.Text.PrivateFontCollection]::new()
    $fontFiles.AddFontFile((Resolve-Path 'Assets/Game/UI/InventoryFolder/Fonts/CourierPrime-Regular.ttf').Path)
    $courier = $fontFiles.Families[0]
    $ink = [Drawing.Color]::FromArgb(47, 47, 50)
    $red = [Drawing.Color]::FromArgb(112, 47, 34)
    $rule = [Drawing.Color]::FromArgb(151, 133, 108)
    $utf8 = [Text.UTF8Encoding]::new($false)
    $lf = [string][char]10
    $format = [Drawing.StringFormat]::GenericTypographic
    $metaTemplate = Get-Content "$chapter/ShutterSequence_Paper.png.meta" -Raw

    function Font($family, $size, $style = [Drawing.FontStyle]::Regular) {
        [Drawing.Font]::new($family, $size, $style, [Drawing.GraphicsUnit]::Pixel)
    }

    function Save-Art($path, $guid, $bitmap, [switch]$Readable) {
        if ($guid -cnotmatch '^[a-f0-9]{32}$') {
            throw "Invalid Unity asset GUID for $path ($guid)"
        }
        $target = Join-Path (Get-Location) $path
        $bitmap.Save($target, [Drawing.Imaging.ImageFormat]::Png)
        $meta = $metaTemplate -replace 'guid: [a-f0-9]{32}', "guid: $guid"
        if ($Readable) {
            $meta = $meta -replace 'isReadable: 0', 'isReadable: 1' -replace 'textureType: 8', 'textureType: 0'
            $meta = $meta -replace 'spriteMode: 1', 'spriteMode: 0' -replace 'alphaIsTransparency: 1', 'alphaIsTransparency: 0'
        }
        [IO.File]::WriteAllText("$target.meta", $meta, $utf8)
    }

    function Paper($height, $dark, $light) {
        $bitmap = [Drawing.Bitmap]::new(800, $height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [Drawing.Graphics]::FromImage($bitmap)
        $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $gradient = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(0, 0, 800, $height), $light, $dark, 36.0)
        $g.FillRectangle($gradient, 0, 0, 800, $height)
        $gradient.Dispose()
        $rng = [Random]::new(714)
        $grain = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(27, 118, 90, 63))
        for ($i = 0; $i -lt 5400; $i++) {
            $g.FillRectangle($grain, $rng.Next(800), $rng.Next($height), $rng.Next(1, 4), 1)
        }
        $grain.Dispose()
        @{ Bitmap = $bitmap; Graphics = $g }
    }

    function Print-Line($g, $line, $font, $color, $x, $y) {
        $brush = [Drawing.SolidBrush]::new($color)
        $g.DrawString($line, $font, $brush, [single]$x, [single]$y, $format)
        $brush.Dispose()
    }

    function Rule($g, $color, $x1, $y, $x2) {
        $pen = [Drawing.Pen]::new($color, 2)
        $g.DrawLine($pen, $x1, $y, $x2, $y)
        $pen.Dispose()
    }

    function Print-Paragraph($g, $body, $font, $color, $x, $y, $width, $leading) {
        foreach ($paragraph in $body.Split([char]10)) {
            if (!$paragraph.Trim()) { $y += [int]($leading * .55); continue }
            $line = ''
            foreach ($word in ($paragraph.Trim() -split '\s+')) {
                $candidate = "$line $word".Trim()
                if ($line -and $g.MeasureString($candidate, $font, 10000, $format).Width -gt $width) {
                    Print-Line $g $line $font $color $x $y
                    $line = $word
                    $y += $leading
                } else { $line = $candidate }
            }
            Print-Line $g $line $font $color $x $y
            $y += $leading
        }
        return $y
    }

    function Read-PrintText($section) {
        $value = [regex]::Match($section, '(?ms)^      Unlocalized:[ ]*(.*?)\r?\n      Localized:').Groups[1].Value.Trim()
        if (!$value) { return '' }
        $value = [regex]::Replace($value, '\r?\n *\r?\n[ \r\n]*', { param($m)
            [string]::new([char]10, ([regex]::Matches($m.Value, '\n').Count - 1))
        })
        $value = $value -replace '\r?\n +', ' '
        if ($value.StartsWith('"')) { $value = (ConvertFrom-Json ('{"value":' + $value + '}')).value }
        if ($value.StartsWith("'") -and $value.EndsWith("'")) {
            $value = $value.Substring(1, $value.Length - 2).Replace("''", "'")
        }
        return ($value -replace '<[^>]+>', '')
    }

    function Render-Document($path, $guids, $style) {
        $absolute = (Resolve-Path $path).Path
        $data = [IO.File]::ReadAllText($absolute)
        $pages = [regex]::Matches($data, '(?ms)^  - Text:\r?\n(.*?)^    Image:.*?^    Caption:\r?\n(.*?)^    Text_DEPRECATED:')
        if ($pages.Count -ne $guids.Count) { throw "Unexpected page count in $path" }
        $index = 0
        foreach ($page in $pages) {
            $body = Read-PrintText $page.Groups[1].Value
            if (!$body) { throw "Missing page text in $path" }
            $caption = Read-PrintText $page.Groups[2].Value
            $artName = [IO.Path]::GetFileNameWithoutExtension($path) + "_Page$($index + 1)"
            $paper = Paper 1000 ([Drawing.Color]::FromArgb(209, 186, 147)) ([Drawing.Color]::FromArgb(246, 233, 204))
            $g = $paper.Graphics
            $heading = Font 'Georgia' 44 ([Drawing.FontStyle]::Bold)
            $signature = Font 'Segoe Print' 28
            if ($style -eq 'newspaper') {
                Print-Line $g 'THE CITY HERALD' $heading $ink 56 58
                Rule $g $ink 60 133 740
                Rule $g $ink 60 143 740
                $lines = $body.Split([char]10)
                if ($index -eq 0) { $lines = $lines | Select-Object -Skip 1 }
                $headline = Font 'Georgia' 33 ([Drawing.FontStyle]::Bold)
                $font = Font 'Georgia' 30
                $y = Print-Paragraph $g $lines[0] $headline $red 58 184 681 43
                $null = Print-Paragraph $g (($lines | Select-Object -Skip 1) -join $lf) $font $ink 58 ($y + 22) 681 42
                $headline.Dispose()
            } elseif ($style -in @('schedule', 'circuit')) {
                $font = Font $courier 32
                $titleText = if ($style -eq 'schedule') { 'MAINTENANCE SCHEDULE' } else { 'TABLET CIRCUIT UPDATE' }
                Print-Line $g $titleText $heading $ink 55 71
                Rule $g $rule 61 151 740
                $y = Print-Paragraph $g $body $font $ink 56 210 686 47
                if ($caption) { Print-Line $g $caption $signature $ink 54 ($y + 20) }
            } else {
                $font = Font 'Segoe Print' 34
                for ($y = 164; $y -lt 902; $y += 58) { Rule $g $rule 57 $y 742 }
                $y = Print-Paragraph $g $body $font $ink 55 169 679 58
                if ($caption) { Print-Line $g $caption $signature $ink 57 ($y + 28) }
            }
            $heading.Dispose(); $font.Dispose(); $signature.Dispose(); $g.Dispose()
            Save-Art (Join-Path (Split-Path $path) "$artName.png") $guids[$index] $paper.Bitmap
            $paper.Bitmap.Dispose()
            $index++
        }
        $data = [regex]::Replace($data, '(?m)^    Artwork:.*\r?\n', '')
        $guidQueue = [Collections.Generic.Queue[string]]::new()
        foreach ($guid in $guids) { $guidQueue.Enqueue($guid) }
        $data = [regex]::Replace($data, '(?m)^    Text_DEPRECATED:', {
            "    Artwork: {fileID: 21300000, guid: $($guidQueue.Dequeue()), type: 3}" + $lf + '    Text_DEPRECATED:'
        })
        [IO.File]::WriteAllText($absolute, $data, $utf8)
    }

    Render-Document "$chapter/MaintenanceSchedule.asset" @('f86dea7db09b4374b14d98c79fb24582','8eb55b8f947b4b049e1bffcc383b6ad5') 'schedule'
    Render-Document "$chapter/ElectriciansNote.asset" @('ad508a69dbb849e680d873e4bc3b0bff') 'note'
    Render-Document "$chapter/StorageKeyNote.asset" @('969c929947d748f8bc44c09c16eca676') 'note'
    Render-Document "$chapter/DepotAccessNote.asset" @('0f744586430d4e35918a042b508bf401') 'note'
    Render-Document 'Assets/Game/Levels/Chapter1/MapData/DistrictStorage_IntegratedEntranceDiagram.asset' @('c60a1943be234d45ac52708ccb08ddff') 'circuit'
    Render-Document "$newspaper/Newspaper_CityHerald.asset" @('738d26ebe77a45ddb67c5352a1a511d1','3cbc5ce569474978bbc0eaeb79c09148','b7760a53eab04915ae2f26ef7800a957') 'newspaper'

    $source = Get-Content 'Assets/Game/Levels/Chapter1/Scripts/CorridorClueArtwork.cs' -Raw
    $title = Font $courier 39
    $bodyFont = Font $courier 38
    $glyphFont = Font $courier 40
    $signoff = [regex]::Match($source, 'Signature = "(.*?)"').Groups[1].Value
    foreach ($variant in @(
        @{ Field = 'UnitIntro'; File = 'CorridorClue_UnitPaper'; Guid = 'e1a326f98d44471ebdc6a9cc551425af' },
        @{ Field = 'TenantIntro'; File = 'CorridorClue_TenantPaper'; Guid = '8119ac711ba048d4bb917e1f39593fb8' }
    )) {
        $paper = Paper 890 ([Drawing.Color]::FromArgb(216, 185, 144)) ([Drawing.Color]::FromArgb(241, 223, 183))
        $g = $paper.Graphics
        Rule $g $red 58 137 742
        Rule $g $rule 58 735 742
        $intro = [regex]::Match($source, "$($variant.Field) = ""(.*?)""").Groups[1].Value
        $null = Print-Paragraph $g $intro $bodyFont $ink 65 176 676 49
        for ($i = 0; $i -lt 4; $i++) {
            $y = 320 + $i * 73
            Rule $g $rule 111 ($y + 32) 728
        }
        Print-Line $g $signoff $title $ink 441 776
        $g.Dispose()
        Save-Art "$chapter/$($variant.File).png" $variant.Guid $paper.Bitmap -Readable
        $paper.Bitmap.Dispose()
    }

    $atlas = [Drawing.Bitmap]::new(384, 192, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($atlas)
    $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $alphabet = [regex]::Match($source, 'Alphabet = "(.*?)"').Groups[1].Value
    for ($i = 0; $i -lt $alphabet.Length; $i++) {
        Print-Line $g $alphabet.Substring($i, 1) $glyphFont $ink (($i % 16) * 24) ([Math]::Floor($i / 16) * 48)
    }
    $g.Dispose()
    Save-Art "$chapter/CorridorClue_Glyphs.png" '76ca879259584ebe8c561765ddf15e81' $atlas -Readable
    $atlas.Dispose()

    $tenantSource = [regex]::Match($source, '(?s)Tenants =\s*\{(.*?)\};').Groups[1].Value
    $tenants = [regex]::Matches($tenantSource, '"(.*?)"') | ForEach-Object { $_.Groups[1].Value }
    $units = @(1, 2, 3, 4, 5, 6, 12, 14, 17, 19, 22, 24)
    $roster = for ($i = 0; $i -lt $tenants.Count; $i++) {
        [pscustomobject]@{ Name = $tenants[$i]; Unit = $units[$i] }
    }
    $roster = $roster | Sort-Object { ($_.Name -split ' ')[-1] }
    $paper = Paper 1000 ([Drawing.Color]::FromArgb(212, 201, 167)) ([Drawing.Color]::FromArgb(241, 234, 207))
    $g = $paper.Graphics
    $rosterFont = Font $courier 31
    Print-Line $g 'STORAGE DEPOT' $title $red 59 61
    Print-Line $g 'Tenant register' $rosterFont $ink 59 119
    Rule $g $red 60 176 739
    Print-Line $g 'TENANT' $rosterFont $ink 61 205
    Print-Line $g 'UNIT' $rosterFont $ink 629 205
    $y = 270
    foreach ($entry in $roster) {
        Print-Line $g $entry.Name $rosterFont $ink 62 $y
        Print-Line $g "$($entry.Unit)" $rosterFont $ink 641 $y
        Rule $g $rule 62 ($y + 39) 739
        $y += 52
    }
    $g.Dispose()
    Save-Art "$chapter/GuardOfficeTenantRegister.png" '5e8d0b732b424a388607fbf079ef647a' $paper.Bitmap
    $paper.Bitmap.Dispose()
    $transcript = (@('STORAGE DEPOT', 'Tenant register', '', 'TENANT / UNIT') +
        ($roster | ForEach-Object { "$($_.Name) / $($_.Unit)" })) -join $lf
    $registerPath = (Resolve-Path "$chapter/GuardOfficeTenantRegister.asset").Path
    $register = [IO.File]::ReadAllText($registerPath)
    $textLine = '      Unlocalized: ' + ($transcript | ConvertTo-Json -Compress)
    $register = [regex]::Replace($register, '(?m)^      Unlocalized: ".*"\r?$', $textLine)
    [IO.File]::WriteAllText($registerPath, $register, $utf8)
    $title.Dispose(); $bodyFont.Dispose(); $glyphFont.Dispose(); $rosterFont.Dispose(); $fontFiles.Dispose()
} finally {
    Pop-Location
}
