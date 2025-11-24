param(
    [string]$PackagePath = ".",
    [string]$NugetSource = "https://api.nuget.org/v3/index.json",
    [string]$ApiKey = "myapikey",
    [int]$Timeout = 300
)

function Get-LatestNuGetPackages {
    param([string]$SearchPath)
    
    $allPackages = Get-ChildItem -Path $SearchPath -Filter "*.nupkg" -Recurse
    $packageGroups = @{}
    
    Write-Host "Found $($allPackages.Count) nupkg files total" -ForegroundColor Gray
    
    # 按包名分组
    foreach ($package in $allPackages) {
        # 从文件名解析包名和版本（格式：PackageName.Version.nupkg）
        $fileName = $package.BaseName
        
        # 使用正则表达式匹配包名和版本
        if ($fileName -match '^(.+?)\.(\d+\.\d+\.\d+(-[^\.]+)?)$') {
            $packageName = $matches[1]
            $versionString = $matches[2]
            
            if (-not $packageGroups.ContainsKey($packageName)) {
                $packageGroups[$packageName] = @()
            }
            
            Write-Host "  - $packageName v$versionString" -ForegroundColor DarkGray
            $packageGroups[$packageName] += @{
                File = $package
                Name = $packageName
                VersionString = $versionString
                FullName = $package.FullName
            }
        } else {
            Write-Host "  - Skipped (invalid format): $fileName" -ForegroundColor DarkYellow
        }
    }
    
    # 为每个包选择最新版本（使用正确的版本比较）
    $latestPackages = @()
    foreach ($packageName in $packageGroups.Keys) {
        $versions = $packageGroups[$packageName]
        
        if ($versions.Count -eq 1) {
            # 只有一个版本，直接使用
            $latestPackages += $versions[0]
        } else {
            # 多个版本，需要比较
            $latestVersion = $versions[0]
            
            for ($i = 1; $i -lt $versions.Count; $i++) {
                $current = $versions[$i]
                $compareResult = Compare-NuGetVersion -Version1 $latestVersion.VersionString -Version2 $current.VersionString
                
                if ($compareResult -eq -1) {
                    $latestVersion = $current
                }
            }
            
            $latestPackages += $latestVersion
        }
    }
    
    return $latestPackages
}

function Compare-NuGetVersion {
    param(
        [string]$Version1,
        [string]$Version2
    )
    
    # 使用 System.Version 进行比较（适用于基本版本号）
    try {
        # 处理可能包含预发布标签的版本
        $v1Clean = $Version1.Split('-')[0]
        $v2Clean = $Version2.Split('-')[0]
        
        $ver1 = [System.Version]::new($v1Clean)
        $ver2 = [System.Version]::new($v2Clean)
        
        $result = $ver1.CompareTo($ver2)
        
        # 如果基本版本相同，比较完整字符串（包含预发布标签）
        if ($result -eq 0 -and $Version1 -ne $Version2) {
            # 没有预发布标签的版本比有预发布标签的版本新
            $hasPrerelease1 = $Version1.Contains('-')
            $hasPrerelease2 = $Version2.Contains('-')
            
            if ($hasPrerelease1 -and -not $hasPrerelease2) {
                return -1
            } elseif (-not $hasPrerelease1 -and $hasPrerelease2) {
                return 1
            } else {
                # 都有预发布标签，按字符串比较
                return $Version1.CompareTo($Version2)
            }
        }
        
        return $result
    }
    catch {
        # 如果 Version 解析失败，回退到字符串比较
        Write-Warning "Version comparison failed for '$Version1' vs '$Version2', using string comparison"
        return $Version1.CompareTo($Version2)
    }
}

Write-Host "Scanning for latest NuGet packages in: $PackagePath" -ForegroundColor Cyan

# 获取所有最新版本的包
$latestPackages = Get-LatestNuGetPackages -SearchPath $PackagePath

if ($latestPackages.Count -eq 0) {
    Write-Host "No valid .nupkg files found in the specified directory and subdirectories." -ForegroundColor Red
    exit 1
}

Write-Host "`nFound $($latestPackages.Count) latest version packages to upload:" -ForegroundColor Cyan
foreach ($package in $latestPackages) {
    Write-Host "  - $($package.Name) v$($package.VersionString)" -ForegroundColor White
}

$successCount = 0
$failCount = 0

# 上传最新版本的包
foreach ($package in $latestPackages) {
    Write-Host "`nUploading $($package.Name) v$($package.VersionString)..." -ForegroundColor Yellow
    Write-Host "File: $($package.File.FullName)" -ForegroundColor Gray
    
    $result = dotnet nuget push $package.File.FullName --source $NugetSource --api-key $ApiKey --skip-duplicate --timeout $Timeout
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Successfully uploaded $($package.Name) v$($package.VersionString)" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "✗ Failed to upload $($package.Name) v$($package.VersionString)" -ForegroundColor Red
        $failCount++
    }
}

Write-Host "`nUpload Summary:" -ForegroundColor Cyan
Write-Host "Successful: $successCount" -ForegroundColor Green
Write-Host "Failed: $failCount" -ForegroundColor Red

if ($failCount -gt 0) {
    exit 1
}