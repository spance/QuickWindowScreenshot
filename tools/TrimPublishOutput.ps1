param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDir
)

$ErrorActionPreference = 'Stop'
$PublishDir = $PublishDir.Trim('"', "'")
$resolvedPublishDir = (Resolve-Path -LiteralPath $PublishDir).Path
$allowedCultures = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$allowedCultures.Add('en-US') | Out-Null
$allowedCultures.Add('zh-CN') | Out-Null

Get-ChildItem -LiteralPath $resolvedPublishDir -Directory |
    Where-Object { $_.Name -like '*-*' -and -not $allowedCultures.Contains($_.Name) } |
    Remove-Item -Recurse -Force

$redundantFilePatterns = @(
    'DirectML.dll',
    '*.pdb',
    'Microsoft.UI.Designer.dll',
    'Microsoft.UI.Xaml.Phone.dll',
    'Microsoft.Web.WebView2.Core.dll',
    'Microsoft.Web.WebView2.Core.Projection.dll',
    'Microsoft.ML.OnnxRuntime.dll',
    'onnxruntime.dll',
    'Microsoft.Graphics.Imaging*',
    'Microsoft.Graphics.Internal.Imaging*',
    'Microsoft.Windows.AI.*',
    'Microsoft.Security.Authentication.OAuth*',
    'Microsoft.Windows.ApplicationModel.Background*',
    'Microsoft.Windows.ImageCreationInternal*',
    'Microsoft.Windows.Internal.AI*',
    'Microsoft.Windows.Internal.ImageCreation*',
    'Microsoft.Windows.Internal.SemanticSearch*',
    'Microsoft.Windows.Internal.Vision*',
    'Microsoft.Windows.AppNotifications*',
    'Microsoft.Windows.BadgeNotifications*',
    'Microsoft.Windows.Media.Capture*',
    'Microsoft.Windows.Private.Workloads*',
    'Microsoft.Windows.PushNotifications*',
    'Microsoft.Windows.Security.AccessControl*',
    'Microsoft.Windows.SemanticSearch*',
    'Microsoft.Windows.Storage.Pickers*',
    'Microsoft.Windows.Storage.*',
    'Microsoft.Windows.System.Power*',
    'Microsoft.Windows.Vision*',
    'Microsoft.Windows.Workloads*',
    'Microsoft.Windows.Widgets*',
    'NPUDetect.dll',
    'PerceptiveStreaming.dll',
    'PushNotificationsLongRunningTask.ProxyStub.dll',
    'RestartAgent.exe',
    'SessionHandleIPCProxyStub.dll',
    'System.Numerics.Tensors.dll',
    'WebView2Loader.dll',
    'workloads*.json'
)

foreach ($pattern in $redundantFilePatterns) {
    Get-ChildItem -LiteralPath $resolvedPublishDir -File -Filter $pattern |
        Remove-Item -Force
}

$redundantRelativeFiles = @(
    'Microsoft.UI.Xaml\Assets\map.html',
    'Microsoft.UI.Xaml\Assets\NoiseAsset_256x256_PNG.png',
    'en-us\Microsoft.UI.Xaml.Phone.dll.mui',
    'zh-CN\Microsoft.UI.Xaml.Phone.dll.mui'
)

foreach ($relativeFile in $redundantRelativeFiles) {
    $path = Join-Path $resolvedPublishDir $relativeFile
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

Get-ChildItem -LiteralPath $resolvedPublishDir -Directory -Recurse |
    Sort-Object FullName -Descending |
    Where-Object { -not (Get-ChildItem -LiteralPath $_.FullName -Force) } |
    Remove-Item -Force
