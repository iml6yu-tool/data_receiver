<#
.SYNOPSIS
    把当前目录各项目编译生成的 DLL/PDB 复制到当前目录的 Libs 目录中。

.DESCRIPTION
    以参考目录（ReferenceLibs）的目录结构为准，只处理其中以 "iml6yu" 开头的文件：
    在参考目录里找到某个子目录下的 iml6yu 文件（例如 Mqtt\iml6yu.DataReceive.Mqtt.dll），
    就到当前项目中按文件名匹配对应的编译产物（bin 输出），复制到 Libs 下相同的相对路径。

.PARAMETER ReferenceLibs
    参考目录，提供目标目录结构与需要复制的文件名清单。

.PARAMETER SourceRoot
    产物扫描根目录，默认当前脚本所在目录。

.PARAMETER Destination
    目标目录，默认当前目录下的 Libs。

.PARAMETER Configuration
    首选编译配置（Debug/Release），默认 Release。缺失时回退到其它配置。

.PARAMETER TargetFramework
    首选目标框架（如 net9.0）。为空时按 net9.0 > net8.0 > net6.0 自动优先。

.EXAMPLE
    pwsh -File .\copy_libs.ps1
.EXAMPLE
    pwsh -File .\copy_libs.ps1 -Configuration Debug -TargetFramework net8.0
#>
param(
    [string]$ReferenceLibs = "E:\sources\boshiac\DataCenterClustersV2\src\Host\iml6yu.DataCenter.Worker\Libs",
    [string]$SourceRoot = $PSScriptRoot,
    [string]$Destination = (Join-Path $PSScriptRoot "Libs"),
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net8.0"
)

$ErrorActionPreference = "Stop"

# 参考目录必须以 iml6yu 开头的文件作为目标清单
if (-not (Test-Path -LiteralPath $ReferenceLibs)) {
    Write-Host "参考目录不存在: $ReferenceLibs" -ForegroundColor Red
    exit 1
}

$destFull = [System.IO.Path]::GetFullPath($Destination)

# 目标框架优先级列表（显式声明为字符串数组，避免单元素时被展开成字符串）
[string[]]$tfmOrder = if (-not [string]::IsNullOrWhiteSpace($TargetFramework)) {
    @($TargetFramework)
} else {
    @('net9.0', 'net8.0', 'net6.0')
}

# 扫描当前项目中 bin 目录下的编译产物（排除 obj 与目标 Libs 目录本身）
# 指定了 TargetFramework 时，只保留该框架目录下的产物，其它框架版本一律不参与
$candidates = Get-ChildItem -LiteralPath $SourceRoot -Recurse -File | Where-Object {
    $_.Name -like 'iml6yu*' -and
    ($_.FullName -match '\\bin\\') -and
    ($_.FullName -notlike "$destFull*") -and
    ([string]::IsNullOrWhiteSpace($TargetFramework) -or $_.FullName -match "\\$([regex]::Escape($TargetFramework))\\")
}

# 按文件名分组，便于按文件名匹配
$sourceByName = @{}
foreach ($f in $candidates) {
    if (-not $sourceByName.ContainsKey($f.Name)) {
        $sourceByName[$f.Name] = @()
    }
    $sourceByName[$f.Name] += $f
}

# 计算候选文件的优先分：分数越低越优先
function Get-CandidateScore {
    param([System.IO.FileInfo]$File)

    $score = 0
    $path = $File.FullName

    # 1. 编译配置优先
    if ($path -match "\\bin\\$([regex]::Escape($Configuration))\\") {
        $score += 0
    } else {
        $score += 1000
    }

    # 2. 目标框架优先
    $matchedTfm = $false
    for ($i = 0; $i -lt $tfmOrder.Count; $i++) {
        if ($path -match "\\$([regex]::Escape($tfmOrder[$i]))\\") {
            $score += $i
            $matchedTfm = $true
            break
        }
    }
    if (-not $matchedTfm) { $score += 100 }

    return $score
}

# 只保留参考目录中以 iml6yu 开头的文件，并保留其相对路径（即目录结构）
$targets = Get-ChildItem -LiteralPath $ReferenceLibs -Recurse -File |
    Where-Object { $_.Name -like 'iml6yu*' } |
    ForEach-Object {
        [PSCustomObject]@{
            RelativePath = $_.FullName.Substring($ReferenceLibs.TrimEnd('\').Length + 1)
            Name         = $_.Name
        }
    }

Write-Host "参考目录: $ReferenceLibs" -ForegroundColor Cyan
Write-Host "扫描根目录: $SourceRoot" -ForegroundColor Cyan
Write-Host "目标目录: $Destination" -ForegroundColor Cyan
Write-Host "待处理文件: $($targets.Count) 个（以 iml6yu 开头）" -ForegroundColor Cyan
Write-Host ""

$copied = 0
$missing = @()

foreach ($target in $targets) {
    $destFile = Join-Path $Destination $target.RelativePath

    if (-not $sourceByName.ContainsKey($target.Name)) {
        Write-Host "[缺失] $($target.RelativePath) - 未找到对应的编译产物" -ForegroundColor Red
        $missing += $target.RelativePath
        continue
    }

    # 选取优先级最高的候选，其次取最新修改的
    $best = $sourceByName[$target.Name] |
        Sort-Object @{ Expression = { Get-CandidateScore $_ } }, @{ Expression = { $_.LastWriteTime }; Descending = $true } |
        Select-Object -First 1

    $destDir = Split-Path -Parent $destFile
    if (-not (Test-Path -LiteralPath $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    Copy-Item -LiteralPath $best.FullName -Destination $destFile -Force
    Write-Host "[复制] $($target.RelativePath)  <=  $($best.FullName)" -ForegroundColor Green
    $copied++
}

Write-Host ""
Write-Host "完成：成功复制 $copied 个文件。" -ForegroundColor Cyan
if ($missing.Count -gt 0) {
    Write-Host "以下文件未找到对应产物：" -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    exit 1
}
