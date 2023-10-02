<p align="center"><img src="UABEAvalonia/Assets/logo.png" /></p>

**快速下载:**

[最新夜间版本 (Windows)](https://nightly.link/nesrak1/UABEA/workflows/dotnet-desktop/master/uabea-windows.zip) | [最新夜间版本 (Linux)](https://nightly.link/nesrak1/UABEA/workflows/dotnet-ubuntu/master/uabea-ubuntu.zip) | [最新发布版](https://github.com/nesrak1/UABEA/releases)

[![GitHub问题](https://img.shields.io/github/issues/nesrak1/UABEA?logo=GitHub&style=flat-square)](https://github.com/nesrak1/UABEA/issues) [![discord](https://img.shields.io/discord/862035581491478558?label=discord&logo=discord&logoColor=FFFFFF&style=flat-square)](https://discord.gg/hd9VdswwZs)

## UABEAvalonia

跨平台的资产包/序列化文件读取和写入工具。最初基于（但不是分支自）[UABE](https://github.com/SeriousCache/UABE)。

## 提取资源

开发UABEA更多是作为一种修改/研究工具，而不是提取工具。如果您只想提取资源，请使用[AssetRipper](https://github.com/AssetRipper/AssetRipper)或[AssetStudio](https://github.com/Perfare/AssetStudio/)。

## Addressables

许多游戏现在也使用地址。您可以通过路径`StreamingAssets/aa/XXX/something.bundle`来确定您打开的资产包是否属于地址。[如果您想编辑这些资产包，您需要使用此CRC清理工具清除CRC检查](https://github.com/nesrak1/AddressablesTools/releases)。使用`Example patchcrc catalog.json`，然后移动或重命名旧的catalog.json文件，并将catalog.json.patched重命名为catalog.json。

## 库

- [Avalonia](https://github.com/AvaloniaUI/Avalonia) (MIT 许可证)
  - [Dock.Avalonia](https://github.com/wieslawsoltes/Dock) (MIT 许可证)
  - [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) (MIT 许可证)
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET/tree/upd21-with-inst) (MIT 许可证)
  - [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) (MIT 许可证)
  - [Mono.Cecil](https://github.com/jbevain/cecil) (MIT 许可证)
  - [AssetRipper.TextureDecoder](https://github.com/AssetRipper/TextureDecoder) (MIT 许可证)
- [ISPC Texture Compressor](https://github.com/GameTechDev/ISPCTextureCompressor) (MIT 许可证)
- [Unity crnlib](https://github.com/Unity-Technologies/crunch/tree/unity) (zlib 许可证)
- [PVRTexLib](https://developer.imaginationtech.com/pvrtextool) (PVRTexTool 许可证)
- [ImageSharp](https://github.com/SixLabors/ImageSharp) (Apache 许可证 2.0)
- [Fsb5Sharp](https://github.com/SamboyCoding/Fmod5Sharp) (MIT 许可证)
- [Font Awesome](https://fontawesome.com) (CC BY 4.0 许可证)
