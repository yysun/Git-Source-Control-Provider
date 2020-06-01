del GitSccProvider.wixobj
del GitSccProvider.msi
"%VSSDK90Install%\VisualStudioIntegration\Tools\Wix\candle" GitSccProvider.wxs
"%VSSDK90Install%\VisualStudioIntegration\Tools\Wix\light" GitSccProvider.wixobj