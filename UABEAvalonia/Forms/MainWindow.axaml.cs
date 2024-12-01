using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace UABEAvalonia
{
    public partial class MainWindow : Window
    {
        public BundleWorkspace Workspace { get; }
        public AssetsManager am { get => Workspace.am; }
        public BundleFileInstance BundleInst { get => Workspace.BundleInst; }

        //private Dictionary<string, BundleReplacer> newFiles;
        private bool changesUnsaved; // sets false after saving
        private bool changesMade; // stays true even after saving
        private bool ignoreCloseEvent;
        private List<InfoWindow> openInfoWindows;

        //public ObservableCollection<ComboBoxItem> comboItems;

        public MainWindow()
        {
            // has to happen BEFORE initcomponent
            Workspace = new BundleWorkspace();
            Initialized += MainWindow_Initialized;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            menuOpen.Click += MenuOpen_Click;
            menuLoadPackageFile.Click += MenuLoadPackageFile_Click;
            menuClose.Click += MenuClose_Click;
            menuSave.Click += MenuSave_Click;
            menuSaveAs.Click += MenuSaveAs_Click;
            menuCompress.Click += MenuCompress_Click;
            menuExit.Click += MenuExit_Click;
            menuToggleDarkTheme.Click += MenuToggleDarkTheme_Click;
            menuToggleCpp2Il.Click += MenuToggleCpp2Il_Click;
            menuZygf.Click += MenuZygf_Click;
            menuAbout.Click += MenuAbout_Click;
            btnExport.Click += BtnExport_Click;
            btnImport.Click += BtnImport_Click;
            btnRemove.Click += BtnRemove_Click;
            btnInfo.Click += BtnInfo_Click;
            btnExportAll.Click += BtnExportAll_Click;
            btnImportAll.Click += BtnImportAll_Click;
            btnRename.Click += BtnRename_Click;
            Closing += MainWindow_Closing;

            changesUnsaved = false;
            changesMade = false;
            ignoreCloseEvent = false;
            openInfoWindows = new List<InfoWindow>();

            AddHandler(DragDrop.DropEvent, Drop);

            ThemeHandler.UseDarkTheme = ConfigurationManager.Settings.UseDarkTheme;
        }

        private async void MainWindow_Initialized(object? sender, EventArgs e)
        {
            string classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
            {
                am.LoadClassPackage(classDataPath);
            }
            else
            {
                await MessageBoxUtil.ShowDialog(this, "����", "exe ȱ�� classdata.tpk �ļ���\n��ȷ�������ڡ�");
                Close();
                Environment.Exit(1);
            }
        }

        async void OpenFiles(string[] files)
        {
            string selectedFile = files[0];

            DetectedFileType fileType = FileTypeDetector.DetectFileType(selectedFile);

            await CloseAllFiles();

            // can you even have split bundles?
            if (fileType != DetectedFileType.Unknown)
            {
                if (selectedFile.EndsWith(".split0"))
                {
                    string? splitFilePath = await AskLoadSplitFile(selectedFile);
                    if (splitFilePath == null)
                        return;
                    else
                        selectedFile = splitFilePath;
                }
            }

            if (fileType == DetectedFileType.AssetsFile)
            {
                AssetsFileInstance fileInst = am.LoadAssetsFile(selectedFile, true);

                if (!await LoadOrAskTypeData(fileInst))
                    return;

                List<AssetsFileInstance> fileInstances = new List<AssetsFileInstance>();
                fileInstances.Add(fileInst);

                if (files.Length > 1)
                {
                    for (int i = 1; i < files.Length; i++)
                    {
                        string otherSelectedFile = files[i];
                        DetectedFileType otherFileType = FileTypeDetector.DetectFileType(otherSelectedFile);
                        if (otherFileType == DetectedFileType.AssetsFile)
                        {
                            try
                            {
                                fileInstances.Add(am.LoadAssetsFile(otherSelectedFile, true));
                            }
                            catch
                            {
                                // no warning if the file didn't load but was detected as an assets file
                                // this is so you can select the entire _Data folder and any false positives
                                // don't message the user since it's basically a given
                            }
                        }
                    }
                }

                // shouldn't be possible but just in case
                if (openInfoWindows.Count > 0)
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "����", "������ͬʱ��������Ϣ���ڡ�" +
                               "�������ͬʱ��������ͬ��Ϸ���ļ����뿼�Ǵ����������� UABEA ���ڡ�");

                    return;
                }

                InfoWindow info = new InfoWindow(am, fileInstances, false);
                info.Show();
                info.Closing += (sender, _) =>
                {
                    if (sender == null)
                        return;

                    InfoWindow window = (InfoWindow)sender;
                    openInfoWindows.Remove(window);
                };
                openInfoWindows.Add(info);
            }
            else if (fileType == DetectedFileType.BundleFile)
            {
                BundleFileInstance bundleInst = am.LoadBundleFile(selectedFile, false);

                if (AssetBundleUtil.IsBundleDataCompressed(bundleInst.file))
                {
                    AskLoadCompressedBundle(bundleInst);
                }
                else
                {
                    LoadBundle(bundleInst);
                }
            }
            else
            {
                await MessageBoxUtil.ShowDialog(this, "����", "���ƺ�����һ���ʲ��ļ����������");
            }
        }

        void Drop(object? sender, DragEventArgs e)
        {
            string[] files = e.Data.GetFileNames().ToArray();

            if (files == null || files.Length == 0)
                return;

            OpenFiles(files);
        }

        private async void MenuOpen_Click(object? sender, RoutedEventArgs e)
        {
            var selectedFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "����Դ�������ļ�",
                FileTypeFilter = new List<FilePickerFileType>()
                {
                    new FilePickerFileType("ȫ���ļ�") { Patterns = new List<string>() { "*" } }
                },
                AllowMultiple = true
            });

            string[] selectedFilePaths = FileDialogUtils.GetOpenFileDialogFiles(selectedFiles);
            if (selectedFilePaths.Length == 0)
                return;

            OpenFiles(selectedFilePaths);
        }

        private async void MenuLoadPackageFile_Click(object? sender, RoutedEventArgs e)
        {
            var selectedFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                FileTypeFilter = new List<FilePickerFileType>()
                {
                    new FilePickerFileType("UABE ģ�鰲װ��") { Patterns = new List<string>() { "*.emip" } }
                }
            });

            string[] selectedFilePaths = FileDialogUtils.GetOpenFileDialogFiles(selectedFiles);
            if (selectedFilePaths.Length == 0)
                return;

            string emipPath = selectedFilePaths[0];

            if (emipPath != null && emipPath != string.Empty)
            {
                AssetsFileReader r = new AssetsFileReader(File.OpenRead(emipPath)); //todo close this
                InstallerPackageFile emip = new InstallerPackageFile();
                emip.Read(r);

                LoadModPackageDialog dialog = new LoadModPackageDialog(emip, am);
                await dialog.ShowDialog(this);
            }
        }
        private void MenuZygf_Click(object? sender, RoutedEventArgs e)
        {
            string url = "https://www.aopk.cn/16968.html";
            // ʹ��Ĭ�ϵ� Web �������ָ������ַ
            System.Diagnostics.Process.Start("cmd.exe", "/c start " + url);
        }
        private void MenuAbout_Click(object? sender, RoutedEventArgs e)
        {
            About about = new About();
            about.ShowDialog(this);
        }

        private async void MenuSave_Click(object? sender, RoutedEventArgs e)
        {
            await AskForLocationAndSave(false);
        }

        private async void MenuSaveAs_Click(object? sender, RoutedEventArgs e)
        {
            await AskForLocationAndSave(true);
        }

        private async void MenuCompress_Click(object? sender, RoutedEventArgs e)
        {
            await AskForLocationAndCompress();
        }

        private async void MenuClose_Click(object? sender, RoutedEventArgs e)
        {
            await AskForSave();
            await CloseAllFiles();
        }

        private async void BtnExport_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst == null)
                return;

            BundleWorkspaceItem? item = (BundleWorkspaceItem?)comboBox.SelectedItem;
            if (item == null)
                return;

            var selectedFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "���Ϊ...",
                SuggestedFileName = item.Name
            });

            string? selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
            if (selectedFilePath == null)
                return;

            using FileStream fileStream = File.Open(selectedFilePath, FileMode.Create);

            Stream stream = item.Stream;
            stream.Position = 0;
            stream.CopyToCompat(fileStream, stream.Length);
        }

        private async void BtnImport_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst != null)
            {
                var selectedFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                {
                    Title = "��",
                    FileTypeFilter = new List<FilePickerFileType>()
                    {
                        new FilePickerFileType("ȫ���ļ�") { Patterns = new List<string>() { "*" } }
                    }
                });

                string[] selectedFilePaths = FileDialogUtils.GetOpenFileDialogFiles(selectedFiles);
                if (selectedFilePaths.Length == 0)
                    return;

                string file = selectedFilePaths[0];

                ImportSerializedDialog dialog = new ImportSerializedDialog();
                bool isSerialized = await dialog.ShowDialog<bool>(this);

                byte[] fileBytes = File.ReadAllBytes(file);
                string fileName = Path.GetFileName(file);

                MemoryStream stream = new MemoryStream(fileBytes);
                Workspace.AddOrReplaceFile(stream, fileName, isSerialized);

                SetBundleControlsEnabled(true, true);
                changesUnsaved = true;
                changesMade = true;
            }
        }

        private void BtnRemove_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst != null && comboBox.SelectedItem != null)
            {
                BundleWorkspaceItem? item = (BundleWorkspaceItem?)comboBox.SelectedItem;
                if (item == null)
                    return;

                string origName = item.OriginalName;
                string name = item.Name;
                item.IsRemoved = true;
                Workspace.RemovedFiles.Add(origName);
                Workspace.Files.Remove(item);
                Workspace.FileLookup.Remove(name);

                SetBundleControlsEnabled(true, Workspace.Files.Count > 0);

                changesUnsaved = true;
                changesMade = true;
            }
        }

        private async void BtnInfo_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst == null)
                return;

            BundleWorkspaceItem? item = (BundleWorkspaceItem?)comboBox.SelectedItem;
            if (item == null)
                return;

            string name = item.Name;

            AssetBundleFile bundleFile = BundleInst.file;

            Stream assetStream = item.Stream;

            DetectedFileType fileType = FileTypeDetector.DetectFileType(new AssetsFileReader(assetStream), 0);
            assetStream.Position = 0;

            if (fileType == DetectedFileType.AssetsFile)
            {
                string assetMemPath = Path.Combine(BundleInst.path, name);
                AssetsFileInstance fileInst = am.LoadAssetsFile(assetStream, assetMemPath, true);

                if (BundleInst != null && fileInst.parentBundle == null)
                    fileInst.parentBundle = BundleInst;

                if (!await LoadOrAskTypeData(fileInst))
                    return;

                // don't check for info open here
                // we're assuming it's fine since two infos can
                // be opened from a bundle without problems

                InfoWindow info = new InfoWindow(am, new List<AssetsFileInstance> { fileInst }, true);
                info.Closing += InfoWindow_Closing;
                info.Show();
                openInfoWindows.Add(info);
            }
            else
            {
                if (item.IsSerialized)
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "����", "���ƺ�����һ����Ч���ʲ��ļ������ܸ��ʲ������л���Ҳ���ļ����𻵻�汾���£�");
                }
                else
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "����", "���ƺ�����һ����Ч���ʲ��ļ���������뵼�����ʲ��ļ�����ʹ�õ������ܡ�");
                }
            }
        }

        private async void BtnExportAll_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst == null)
                return;

            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "ѡ�񵼳�Ŀ¼"
            });

            string[]? selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return;

            string dir = selectedFolderPaths[0];

            for (int i = 0; i < BundleInst.file.BlockAndDirInfo.DirectoryInfos.Length; i++)
            {
                AssetBundleDirectoryInfo dirInf = BundleInst.file.BlockAndDirInfo.DirectoryInfos[i];

                string bunAssetName = dirInf.Name;
                string bunAssetPath = Path.Combine(dir, bunAssetName);

                // create dirs if bundle contains / in path
                if (bunAssetName.Contains("\\") || bunAssetName.Contains("/"))
                {
                    string bunAssetDir = Path.GetDirectoryName(bunAssetPath);
                    if (!Directory.Exists(bunAssetDir))
                    {
                        Directory.CreateDirectory(bunAssetDir);
                    }
                }

                using FileStream fileStream = File.Open(bunAssetPath, FileMode.Create);

                AssetsFileReader bundleReader = BundleInst.file.DataReader;
                bundleReader.Position = dirInf.Offset;
                bundleReader.BaseStream.CopyToCompat(fileStream, dirInf.DecompressedSize);
            }
        }

        private async void BtnImportAll_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst == null)
                return;

            var selectedFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "ѡ����Ŀ¼"
            });

            string[]? selectedFolderPaths = FileDialogUtils.GetOpenFolderDialogFiles(selectedFolders);
            if (selectedFolderPaths.Length == 0)
                return;

            string dir = selectedFolderPaths[0];

            foreach (string filePath in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(dir, filePath);
                relPath = relPath.Replace("\\", "/").TrimEnd('/');

                BundleWorkspaceItem? itemToReplace = Workspace.Files.FirstOrDefault(f => f.Name == relPath);
                if (itemToReplace != null)
                {
                    Workspace.AddOrReplaceFile(File.OpenRead(filePath), itemToReplace.Name, itemToReplace.IsSerialized);
                }
                else
                {
                    DetectedFileType type = FileTypeDetector.DetectFileType(filePath);
                    bool isSerialized = type == DetectedFileType.AssetsFile;
                    Workspace.AddOrReplaceFile(File.OpenRead(filePath), relPath, isSerialized);
                }
            }

            changesUnsaved = true;
            changesMade = true;
        }

        private async void BtnRename_Click(object? sender, RoutedEventArgs e)
        {
            if (BundleInst == null)
                return;

            BundleWorkspaceItem? item = (BundleWorkspaceItem?)comboBox.SelectedItem;
            if (item == null)
                return;

            // if we rename twice, the "original name" is the current name
            RenameWindow window = new RenameWindow(item.Name);
            string newName = await window.ShowDialog<string>(this);
            if (newName == string.Empty)
                return;

            Workspace.RenameFile(item.Name, newName);

            // reload the text in the selected item preview
            // why not just use propertychangeevent? it's because getting
            // events working and the fact that displaymemberpath isn't
            // supported means more trouble than it's worth. this hack is
            // good enough, despite being jank af.
            Workspace.Files.Add(null);
            comboBox.SelectedItem = null;
            comboBox.SelectedItem = item;
            Workspace.Files.Remove(null);

            changesUnsaved = true;
            changesMade = true;
        }

        private void MenuExit_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuToggleDarkTheme_Click(object? sender, RoutedEventArgs e)
        {
            ConfigurationManager.Settings.UseDarkTheme = !ConfigurationManager.Settings.UseDarkTheme;
            ThemeHandler.UseDarkTheme = ConfigurationManager.Settings.UseDarkTheme;
        }

        private async void MenuToggleCpp2Il_Click(object? sender, RoutedEventArgs e)
        {
            bool useCpp2Il = !ConfigurationManager.Settings.UseCpp2Il;
            ConfigurationManager.Settings.UseCpp2Il = useCpp2Il;

            await MessageBoxUtil.ShowDialog(this, "ע��",
                $"Cpp2Il ʹ��״̬��{useCpp2Il.ToString().ToLower()}");
        }

        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!changesUnsaved || ignoreCloseEvent)
            {
                e.Cancel = false;
                ignoreCloseEvent = false;
            }
            else
            {
                e.Cancel = true;
                ignoreCloseEvent = true;

                await AskForSave();
                Close(); // calling Close() triggers Closing() again
            }
        }

        private void InfoWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sender == null)
                return;

            InfoWindow window = (InfoWindow)sender;
            openInfoWindows.Remove(window);

            if (window.Workspace.fromBundle && window.ChangedAssetsDatas != null)
            {
                List<Tuple<AssetsFileInstance, byte[]>> assetDatas = window.ChangedAssetsDatas;

                foreach (var tup in assetDatas)
                {
                    AssetsFileInstance fileInstance = tup.Item1;
                    byte[] assetData = tup.Item2;

                    // remember selected index, when we replace the file it unselects the combobox item
                    int comboBoxSelectedIndex = comboBox.SelectedIndex;

                    string assetName = Path.GetFileName(fileInstance.path);
                    Workspace.AddOrReplaceFile(new MemoryStream(assetData), assetName, true);
                    // unload it so the new version is reloaded when we reopen it
                    am.UnloadAssetsFile(fileInstance.path);

                    // reselect the combobox item
                    comboBox.SelectedIndex = comboBoxSelectedIndex;
                }

                if (assetDatas.Count > 0)
                {
                    changesUnsaved = true;
                    changesMade = true;
                }
            }
        }

        private async Task<bool> LoadOrAskTypeData(AssetsFileInstance fileInst)
        {
            string uVer = fileInst.file.Metadata.UnityVersion;
            if (uVer == "0.0.0" && fileInst.parentBundle != null)
            {
                uVer = fileInst.parentBundle.file.Header.EngineVersion;
            }

            if (uVer == "0.0.0")
            {
                VersionWindow window = new VersionWindow(uVer);
                uVer = await window.ShowDialog<string>(this);
                if (uVer == string.Empty)
                {
                    if (!fileInst.file.Metadata.TypeTreeEnabled)
                    {
                        // if we have no type tree, there's no way we're loading anything
                        await MessageBoxUtil.ShowDialog(this, "Error", "You must enter a Unity version to load a typetree-stripped file.");
                        return false;
                    }
                    else
                    {
                        // bad, but we can at least rely on the type tree for most things
                        uVer = "0.0.0";
                    }
                }
            }

            am.LoadClassDatabaseFromPackage(uVer);
            return true;
        }

        private async Task AskForLocationAndSave(bool saveAs)
        {
            if (changesUnsaved && BundleInst != null)
            {
                if (saveAs)
                {
                    var selectedFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
                    {
                        Title = "���Ϊ..."
                    });

                    string? selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
                    if (selectedFilePath == null)
                        return;

                    if (Path.GetFullPath(selectedFilePath) == Path.GetFullPath(BundleInst.path))
                    {
                        await MessageBoxUtil.ShowDialog(this,
                            "�ļ�����ʹ����", "��ʹ�á��ļ��� > �����桱�����ǵ�ǰ�򿪵İ��ļ���");
                        return;
                    }

                    try
                    {
                        SaveBundle(BundleInst, selectedFilePath);
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxUtil.ShowDialog(this,
                            "д���쳣", "��д���ļ�ʱ�������⣺\n" + ex.ToString());
                    }
                }
                else
                {
                    try
                    {
                        SaveBundleOver(BundleInst);
                    }
                    catch (Exception ex)
                    {
                        await MessageBoxUtil.ShowDialog(this,
                            "д���쳣", "��д���ļ�ʱ�������⣺\n" + ex.ToString());
                    }
                }
            }
        }

        private async Task AskForSave()
        {
            if (changesUnsaved && BundleInst != null)
            {
                MessageBoxResult choice = await MessageBoxUtil.ShowDialog(this,
                    "����������", "�����޸Ĵ��ļ����Ƿ�Ҫ���棿",
                    MessageBoxType.YesNo);
                if (choice == MessageBoxResult.Yes)
                {
                    await AskForLocationAndSave(true);
                }
            }
        }

        private async Task AskForLocationAndCompress()
        {
            if (BundleInst != null)
            {
                // temporary, maybe I should just write to a memory stream or smth
                // edit: looks like uabe just asks you to open a file instead of
                // using your currently opened one, so that may be the workaround
                if (changesMade)
                {
                    string messageBoxTest;
                    if (changesUnsaved)
                    {
                        messageBoxTest =
                            "���Ѿ��޸��˴��ļ�������δ���������ļ����浽���̡� \n" +
                            "�����Ҫѹ�����и��ĵ��ļ�������������������ļ����򿪸��ļ��� \n" +
                            "������ȷ������ѹ���������ĵ��ļ���";
                    }
                    else
                    {
                        messageBoxTest =
                            "���Ѿ��޸��˴��ļ�����ֻ���ڽ��и���֮ǰ�ľ��ļ��Ѵ򿪡� \n" +
                            "�����Ҫѹ�����и��ĵ��ļ�����رմ������ļ�������������ļ��� ������ȷ������ѹ���������ĵ��ļ���";
                    }

                    MessageBoxResult continueWithChanges = await MessageBoxUtil.ShowDialog(
                        this, "ע��", messageBoxTest,
                        MessageBoxType.OKCancel);

                    if (continueWithChanges == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                }

                var selectedFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
                {
                    Title = "���Ϊ..."
                });

                string? selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
                if (selectedFilePath == null)
                    return;

                if (Path.GetFullPath(selectedFilePath) == Path.GetFullPath(BundleInst.path))
                {
                    await MessageBoxUtil.ShowDialog(this,
                        "�ļ�����ʹ����", "���ڴ��ļ����� UABEA �д򿪣�������ѡ��һ���µ��ļ�������Ǹ����");
                    return;
                }

                const string lz4Option = "LZ4";
                const string lzmaOption = "LZMA";
                const string cancelOption = "ȡ��";
                string result = await MessageBoxUtil.ShowDialogCustom(
                    this, "��ʾ", "����ʹ������ѹ��������\nLZ4���ٶȸ��쵫�ļ���С�ϴ�\nLZMA���ٶȽ������ļ���С��С",
                    lz4Option, lzmaOption, cancelOption);

                AssetBundleCompressionType compType = result switch
                {
                    lz4Option => AssetBundleCompressionType.LZ4,
                    lzmaOption => AssetBundleCompressionType.LZMA,
                    _ => AssetBundleCompressionType.None
                };

                if (compType != AssetBundleCompressionType.None)
                {
                    ProgressWindow progressWindow = new ProgressWindow("����ѹ��...");

                    Thread thread = new Thread(new ParameterizedThreadStart(CompressBundle));
                    object[] threadArgs =
                    {
                        BundleInst,
                        selectedFilePath,
                        compType,
                        progressWindow.Progress
                    };
                    thread.Start(threadArgs);

                    await progressWindow.ShowDialog(this);
                }
            }
            else
            {
                await MessageBoxUtil.ShowDialog(this, "��ʾ", "����ʹ��ѹ��ǰ��һ�������ļ���");
            }
        }

        private async Task<string?> AskLoadSplitFile(string fileToSplit)
        {
            MessageBoxResult splitRes = await MessageBoxUtil.ShowDialog(this,
                "��⵽����ļ�", "���ļ��� .split0 ��β���Ƿ񴴽��ϲ��ļ���\n",
                MessageBoxType.YesNoCancel);

            if (splitRes == MessageBoxResult.Yes)
            {
                var selectedFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
                {
                    Title = "ѡ��ϲ��ļ���λ��",
                    SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(Path.GetDirectoryName(fileToSplit)!),
                    SuggestedFileName = Path.GetFileName(fileToSplit[..^".split0".Length])
                });

                string? selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
                if (selectedFilePath == null)
                    return null;

                using (FileStream mergeFile = File.Open(selectedFilePath, FileMode.Create))
                {
                    int idx = 0;
                    string thisSplitFileNoNum = fileToSplit.Substring(0, fileToSplit.Length - 1);
                    string thisSplitFileNum = fileToSplit;
                    while (File.Exists(thisSplitFileNum))
                    {
                        using (FileStream thisSplitFile = File.OpenRead(thisSplitFileNum))
                        {
                            thisSplitFile.CopyTo(mergeFile);
                        }

                        idx++;
                        thisSplitFileNum = $"{thisSplitFileNoNum}{idx}";
                    };
                }
                return selectedFilePath;
            }
            else if (splitRes == MessageBoxResult.No)
            {
                return fileToSplit;
            }
            else //if (splitRes == MessageBoxResult.Cancel)
            {
                return null;
            }
        }

        private async void AskLoadCompressedBundle(BundleFileInstance bundleInst)
        {
            string decompSize = FileUtils.GetFormattedByteSize(GetBundleDataDecompressedSize(bundleInst.file));

            const string fileOption = "�ļ�";
            const string memoryOption = "�ڴ�";
            const string cancelOption = "ȡ��";
            string result = await MessageBoxUtil.ShowDialogCustom(
                this, "ע��", "������Ѿ���ѹ����Ҫ��ѹ���ļ������ڴ棿\n��С��" + decompSize,
                fileOption, memoryOption, cancelOption);

            if (result == fileOption)
            {
                string? selectedFilePath;
                while (true)
                {
                    var selectedFile = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
                    {
                        Title = "���Ϊ...",
                        FileTypeChoices = new List<FilePickerFileType>()
                        {
                            new FilePickerFileType("ȫ���ļ�") { Patterns = new List<string>() { "*" } }
                        }
                    });

                    selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
                    if (selectedFilePath == null)
                        return;

                    if (Path.GetFullPath(selectedFilePath) == Path.GetFullPath(bundleInst.path))
                    {
                        await MessageBoxUtil.ShowDialog(this,
                            "�ļ��ѱ�ʹ��", "���ڴ��ļ����� UABEA �д򿪣�������ѡ��һ���µ��ļ�������Ǹ����");
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                DecompressToFile(bundleInst, selectedFilePath);
            }
            else if (result == memoryOption)
            {
                // for lz4 block reading
                if (bundleInst.file.DataIsCompressed)
                {
                    DecompressToMemory(bundleInst);
                }
            }
            else //if (result == cancelOption || result == closeOption)
            {
                return;
            }

            LoadBundle(bundleInst);
        }

        private void DecompressToFile(BundleFileInstance bundleInst, string savePath)
        {
            AssetBundleFile bundle = bundleInst.file;

            FileStream bundleStream = File.Open(savePath, FileMode.Create);
            bundle.Unpack(new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream));

            bundle.Close();
            bundleInst.file = newBundle;
        }

        private void DecompressToMemory(BundleFileInstance bundleInst)
        {
            AssetBundleFile bundle = bundleInst.file;

            MemoryStream bundleStream = new MemoryStream();
            bundle.Unpack(new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream));

            bundle.Close();
            bundleInst.file = newBundle;
        }

        private void LoadBundle(BundleFileInstance bundleInst)
        {
            Workspace.Reset(bundleInst);

            comboBox.ItemsSource = Workspace.Files;
            comboBox.SelectedIndex = 0;

            lblFileName.Text = bundleInst.name;

            SetBundleControlsEnabled(true, Workspace.Files.Count > 0);
        }

        private void SaveBundle(BundleFileInstance bundleInst, string path)
        {
            List<BundleReplacer> replacers = Workspace.GetReplacers();
            using (FileStream fs = File.Open(path, FileMode.Create))
            using (AssetsFileWriter w = new AssetsFileWriter(fs))
            {
                bundleInst.file.Write(w, replacers.ToList());
            }
            changesUnsaved = false;
        }

        private void SaveBundleOver(BundleFileInstance bundleInst)
        {
            string newName = "~" + bundleInst.name;
            string dir = Path.GetDirectoryName(bundleInst.path)!;
            string filePath = Path.Combine(dir, newName);
            string origFilePath = bundleInst.path;

            SaveBundle(bundleInst, filePath);

            // "overwrite" the original
            bundleInst.file.Reader.Close();
            File.Delete(origFilePath);
            File.Move(filePath, origFilePath);
            bundleInst.file = new AssetBundleFile();
            bundleInst.file.Read(new AssetsFileReader(File.OpenRead(origFilePath)));

            BundleWorkspaceItem? selectedItem = (BundleWorkspaceItem?)comboBox.SelectedItem;
            string? selectedName = null;
            if (selectedItem != null)
            {
                selectedName = selectedItem.Name;
            }

            Workspace.Reset(bundleInst);

            BundleWorkspaceItem? newItem = Workspace.Files.FirstOrDefault(f => f.Name == selectedName);
            if (newItem != null)
            {
                comboBox.SelectedItem = newItem;
            }
        }

        private void CompressBundle(object? args)
        {
            object[] argsArr = (object[])args!;

            var bundleInst = (BundleFileInstance)argsArr[0];
            var path = (string)argsArr[1];
            var compType = (AssetBundleCompressionType)argsArr[2];
            var progress = (IAssetBundleCompressProgress)argsArr[3];

            using (FileStream fs = File.Open(path, FileMode.Create))
            using (AssetsFileWriter w = new AssetsFileWriter(fs))
            {
                bundleInst.file.Pack(bundleInst.file.Reader, w, compType, true, progress);
            }
        }

        private async Task CloseAllFiles()
        {
            List<InfoWindow> openInfoWindowsCopy = new List<InfoWindow>(openInfoWindows);
            foreach (InfoWindow window in openInfoWindowsCopy)
            {
                await window.AskForSaveAndClose();
            }

            //newFiles.Clear();
            changesUnsaved = false;
            changesMade = false;

            am.UnloadAllAssetsFiles(true);
            am.UnloadAllBundleFiles();

            SetBundleControlsEnabled(false, true);

            Workspace.Reset(null);

            lblFileName.Text = "����û�д��ļ�";
        }

        private void SetBundleControlsEnabled(bool enabled, bool hasAssets = false)
        {
            // buttons that should be enabled only if there are assets they can interact with
            if (hasAssets)
            {
                btnExport.IsEnabled = enabled;
                btnRemove.IsEnabled = enabled;
                btnRename.IsEnabled = enabled;
                btnInfo.IsEnabled = enabled;
                btnExportAll.IsEnabled = enabled;
            }

            // always enable / disable no matter if there's assets or not
            comboBox.IsEnabled = enabled;
            btnImport.IsEnabled = enabled;
            btnImportAll.IsEnabled = enabled;
        }

        private long GetBundleDataDecompressedSize(AssetBundleFile bundleFile)
        {
            long totalSize = 0;
            foreach (AssetBundleDirectoryInfo dirInf in bundleFile.BlockAndDirInfo.DirectoryInfos)
            {
                totalSize += dirInf.DecompressedSize;
            }
            return totalSize;
        }
    }
}
