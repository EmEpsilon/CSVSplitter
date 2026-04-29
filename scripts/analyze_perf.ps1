param(
    [string]$Configuration = "Release",
    [int]$Repeat = 3
)

$ErrorActionPreference = "Stop"

if ($Repeat -lt 1) {
    throw "Repeat must be >= 1."
}

$project = "CSVSplitter.Tests/CSVSplitter.Tests.csproj"

# 改修案1〜5に関連する代表テストを対象にする
$filter = @(
    "CountCsvFileAsync_ヘッダーを除いたデータ行数を返すこと"
    "EstimateCsvFileRecordsAsync_改行終端とヘッダー有無を考慮して推定できること"
    "ReconcileTotalRecords_推定値と実測値から合計を補正できること"
    "SortRows_しきい値以上は別リストを返してソートすること"
    "MergeCsvFileAsync_複数入力をキー順にマージできること"
    "OutputCsvFileAsync_ダブルクォートを含む値でも分割キーを取得できること"
) -join "|"

Write-Host "== Build ($Configuration) =="
dotnet build $project -c $Configuration | Out-Host

$rows = @()
for ($i = 1; $i -le $Repeat; $i++) {
    Write-Host ""
    Write-Host "== Run $i/$Repeat =="
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet test $project `
        -c $Configuration `
        --no-build `
        --filter $filter `
        --verbosity minimal `
        | Out-Host
    $sw.Stop()

    $rows += [PSCustomObject]@{
        Run = $i
        Milliseconds = $sw.ElapsedMilliseconds
        Seconds = [Math]::Round($sw.Elapsed.TotalSeconds, 3)
    }
}

Write-Host ""
Write-Host "== Summary =="
$rows | Format-Table -AutoSize

$avgMs = ($rows | Measure-Object -Property Milliseconds -Average).Average
$minMs = ($rows | Measure-Object -Property Milliseconds -Minimum).Minimum
$maxMs = ($rows | Measure-Object -Property Milliseconds -Maximum).Maximum

Write-Host ("Average: {0:N0} ms" -f $avgMs)
Write-Host ("Min    : {0:N0} ms" -f $minMs)
Write-Host ("Max    : {0:N0} ms" -f $maxMs)
