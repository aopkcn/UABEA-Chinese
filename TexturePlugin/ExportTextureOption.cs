using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEAvalonia;
using UABEAvalonia.Plugins;

namespace TexturePlugin
{
    public class ExportTextureOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            if (selection.Count > 1)
                name = "Batch export textures";
            else
                name = "Export texture";

            if (action != UABEAPluginAction.Export)
                return false;

            foreach (AssetContainer cont in selection)
            {
                if (cont.ClassId != (int)AssetClassID.Texture2D)
                    return false;
            }
            return true;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            if (selection.Count > 1)
                return await BatchExport(win, workspace, selection);
            else
                return await SingleExport(win, workspace, selection);
        }

        public async Task<bool> BatchExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            for (int i = 0; i < selection.Count; i++)
            {
                selection[i] = new AssetContainer(selection[i], TextureHelper.GetByteArrayTexture(workspace, selection[i]));
            }

            ExportBatchChooseTypeDialog dialog = new ExportBatchChooseTypeDialog();
            string fileType = await dialog.ShowDialog<string>(win);

            if (fileType == null || fileType == string.Empty)
                return false;

            var selectedFolders = await win.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "选择导出目录"
            });

            string[] selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return false;

            string dir = selectedFolderPaths[0];

            StringBuilder errorBuilder = new StringBuilder();

            foreach (AssetContainer cont in selection)
            {
                string errorAssetName = $"{Path.GetFileName(cont.FileInstance.path)}/{cont.PathId}";

                AssetTypeValueField texBaseField = cont.BaseValueField;
                TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);

                //0x0 texture, usually called like Font Texture or smth
                if (texFile.m_Width == 0 && texFile.m_Height == 0)
                    continue;

                string assetName = PathUtils.ReplaceInvalidPathChars(texFile.m_Name);
                string file = Path.Combine(dir, $"{assetName}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.{fileType.ToLower()}");

                //bundle resS
                if (!TextureHelper.GetResSTexture(texFile, cont.FileInstance))
                {
                    string resSName = Path.GetFileName(texFile.m_StreamData.path);
                    errorBuilder.AppendLine($"[{errorAssetName}]: resS was detected but {resSName} was not found in bundle");
                    continue;
                }

                byte[] data = TextureHelper.GetRawTextureBytes(texFile, cont.FileInstance);

                if (data == null)
                {
                    string resSName = Path.GetFileName(texFile.m_StreamData.path);
                    errorBuilder.AppendLine($"[{errorAssetName}]: resS was detected but {resSName} was not found on disk");
                    continue;
                }

                byte[] platformBlob = TextureHelper.GetPlatformBlob(texBaseField);
                uint platform = cont.FileInstance.file.Metadata.TargetPlatform;

                bool success = TextureImportExport.Export(data, file, texFile.m_Width, texFile.m_Height, (TextureFormat)texFile.m_TextureFormat, platform, platformBlob);
                if (!success)
                {
                    string texFormat = ((TextureFormat)texFile.m_TextureFormat).ToString();
                    errorBuilder.AppendLine($"[{errorAssetName}]: Failed to decode texture format {texFormat}");
                    continue;
                }
            }

            if (errorBuilder.Length > 0)
            {
                string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
                string firstLinesStr = string.Join('\n', firstLines);
                await MessageBoxUtil.ShowDialog(win, "导出时发生了一些错误", firstLinesStr);
            }

            return true;
        }

        public async Task<bool> SingleExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            AssetTypeValueField texBaseField = TextureHelper.GetByteArrayTexture(workspace, cont);
            TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);

            // 0x0 texture, usually called like Font Texture or smth
            if (texFile.m_Width == 0 && texFile.m_Height == 0)
            {
                await MessageBoxUtil.ShowDialog(win, "错误", $"纹理大小为0x0。无法导出纹理。");
                return false;
            }

            string assetName = PathUtils.ReplaceInvalidPathChars(texFile.m_Name);

            var selectedFile = await win.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "保存纹理",
                FileTypeChoices = new List<FilePickerFileType>()
                {
                    new FilePickerFileType("PNG 文件") { Patterns = new List<string>() { "*.png" } },
                    new FilePickerFileType("TGA 文件") { Patterns = new List<string>() { "*.tga" } },
                },
                SuggestedFileName = $"{assetName}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}",
                DefaultExtension = "png"
            });

            string selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
            if (selectedFilePath == null)
                return false;

            string errorAssetName = $"{Path.GetFileName(cont.FileInstance.path)}/{cont.PathId}";

            //bundle resS
            if (!TextureHelper.GetResSTexture(texFile, cont.FileInstance))
            {
                string resSName = Path.GetFileName(texFile.m_StreamData.path);
                await MessageBoxUtil.ShowDialog(win, "错误", $"[{errorAssetName}]: 检测到 resS，但在包中未找到 {resSName}");
                return false;
            }

            byte[] data = TextureHelper.GetRawTextureBytes(texFile, cont.FileInstance);

            if (data == null)
            {
                string resSName = Path.GetFileName(texFile.m_StreamData.path);
                await MessageBoxUtil.ShowDialog(win, "错误", $"[{errorAssetName}]: 检测到 resS，但在磁盘上未找到 {resSName}");
                return false;
            }

            byte[] platformBlob = TextureHelper.GetPlatformBlob(texBaseField);
            uint platform = cont.FileInstance.file.Metadata.TargetPlatform;

            bool success = TextureImportExport.Export(data, selectedFilePath, texFile.m_Width, texFile.m_Height, (TextureFormat)texFile.m_TextureFormat, platform, platformBlob);
            if (!success)
            {
                string texFormat = ((TextureFormat)texFile.m_TextureFormat).ToString();
                await MessageBoxUtil.ShowDialog(win, "错误", $"[{errorAssetName}]: 无法解码纹理格式 {texFormat}");
            }
            return success;
        }
    }
}
