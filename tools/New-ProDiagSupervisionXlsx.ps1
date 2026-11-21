param([string]$exportXlsx, [string]$outXlsx)
# Adds one operand supervision row (ProDiag_FB, "Tag_Bool_1", category 1, text "Test supervision") to a V19/V20
# ProDiag export (sheet without the Name/Label columns of V21) and writes a new workbook for supervisionImport.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
Copy-Item $exportXlsx $outXlsx -Force
$z = [System.IO.Compression.ZipFile]::Open($outXlsx, 'Update')
try {
    $e = $z.GetEntry('xl/worksheets/sheet.xml')
    $r = New-Object System.IO.StreamReader($e.Open()); $xml = $r.ReadToEnd(); $r.Close()
    $vals = @('', 'ProDiag_FB', '1', '"Tag_Bool_1"', 'False', 'O', '1', '', '', 'T#0ms', '', 'False', '', 'False', '', 'False', 'Test supervision', '', '', '', '', '')
    $cols = 'ABCDEFGHIJKLMNOPQRSTUV'
    $cells = for ($i = 0; $i -lt $vals.Count; $i++) {
        $v = [System.Security.SecurityElement]::Escape($vals[$i])
        '<x:c r="{0}2" t="inlineStr"><x:is><x:t>{1}</x:t></x:is></x:c>' -f $cols[$i], $v
    }
    $row = '<x:row r="2">' + ($cells -join '') + '</x:row>'
    $xml = $xml.Replace('</x:row></x:sheetData>', '</x:row>' + $row + '</x:sheetData>')
    $e.Delete()
    $n = $z.CreateEntry('xl/worksheets/sheet.xml')
    $w = New-Object System.IO.StreamWriter($n.Open(), (New-Object System.Text.UTF8Encoding $false)); $w.Write($xml); $w.Close()
}
finally { $z.Dispose() }
"written $outXlsx"
