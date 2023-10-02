using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public partial class AssetsFileInfoWindow
    {
        private Dictionary<AssetsFileInstance, List<AssetsFileExternal>> dependencyMap;
        private HashSet<AssetsFileInstance> dependenciesModified;

        private bool askedAboutMoving;

        private void SetupDepsPageEvents()
        {
            btnAdd.Click += BtnAdd_Click;
            btnEdit.Click += BtnEdit_Click;
            btnRemove.Click += BtnRemove_Click;
            btnMoveUp.Click += BtnMoveUp_Click;
            btnMoveDown.Click += BtnMoveDown_Click;
        }

        private void FillDependenciesInfo()
        {
            askedAboutMoving = false;

            UpdateDepsListBox();
        }

        private void UpdateDepsListBox()
        {
            if (cbxFiles.SelectedItem == null)
                return;

            AssetsFileInstance? selectedFile = activeFile;

            if (allMode)
            {
                List<DependencyListBoxItem> lbDeps = new List<DependencyListBoxItem>();
                for (int i = 0; i < workspace.LoadedFiles.Count; i++)
                {
                    lbDeps.Add(new DependencyListBoxItem(i, workspace.LoadedFiles[i]));
                }

                boxDependenciesList.ItemsSource = lbDeps;

                SetButtonsEnabled(false);
            }
            else
            {
                List<AssetsFileExternal> deps = dependencyMap[selectedFile];

                List<DependencyListBoxItem> lbDeps = new List<DependencyListBoxItem>();
                lbDeps.Add(new DependencyListBoxItem(0, selectedFile));
                for (int i = 0; i < deps.Count; i++)
                {
                    lbDeps.Add(new DependencyListBoxItem(i + 1, deps[i]));
                }

                boxDependenciesList.ItemsSource = lbDeps;

                SetButtonsEnabled(true);
            }
        }

        private async void BtnAdd_Click(object? sender, RoutedEventArgs e)
        {
            AssetsFileInstance? selectedFile = activeFile;
            if (allMode)
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "您必须添加依赖项到特定文件，而不是整个工作区。");

                return;
            }

            AddDependencyWindow window = new AddDependencyWindow();
            AssetsFileExternal? dependency = await window.ShowDialog<AssetsFileExternal?>(this);
            if (dependency == null)
            {
                return;
            }

            dependencyMap[selectedFile].Add(dependency);
            dependenciesModified.Add(selectedFile);

            UpdateDepsListBox();
        }

        private async void BtnEdit_Click(object? sender, RoutedEventArgs e)
        {
            AssetsFileInstance? selectedFile = activeFile;
            if (allMode)
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "您必须编辑文件中的依赖项，而不是整个工作区中的依赖项。");

                return;
            }

            DependencyListBoxItem? dependency = GetSelectedDependency();
            if (dependency == null || dependency.dependency == null)
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "您必须选择要编辑的依赖项。");

                return;
            }

            if (!dependency.isDependency)
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "基础文件不是一个依赖项。");

                return;
            }

            AssetsFileExternal oldDependency = dependency.dependency;

            AddDependencyWindow window = new AddDependencyWindow(oldDependency.PathName, oldDependency.OriginalPathName, oldDependency.Type, oldDependency.Guid);
            AssetsFileExternal? newDependency = await window.ShowDialog<AssetsFileExternal?>(this);
            if (newDependency == null)
            {
                return;
            }

            // not trusting dependency.index for now
            int oldDependencyIndex = dependencyMap[selectedFile].IndexOf(oldDependency);
            if (oldDependencyIndex == -1)
                return;

            dependencyMap[selectedFile][oldDependencyIndex] = newDependency;
            dependenciesModified.Add(selectedFile);

            UpdateDepsListBox();
        }

        private async void BtnRemove_Click(object? sender, RoutedEventArgs e)
        {
            AssetsFileInstance? selectedFile = activeFile;
            if (allMode)
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "您必须从文件中删除依赖项，而不是整个工作区。");

                return;
            }

            DependencyListBoxItem? dependency = GetSelectedDependency();
            if (dependency == null)
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "您必须选择要删除的依赖项。");

                return;
            }

            if (!dependency.isDependency)
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "您不能删除基础文件。");

                return;
            }

            int originalDependencyCount = selectedFile.file.Metadata.Externals.Count;
            if (!askedAboutMoving && boxDependenciesList.SelectedIndex <= originalDependencyCount)
            {
                bool shouldContinue = await ShowMoveConfirmationDialog();
                if (!shouldContinue)
                    return;

                askedAboutMoving = true;
            }

            dependencyMap[selectedFile].Remove(dependency.dependency!);
            dependenciesModified.Add(selectedFile);

            UpdateDepsListBox();
        }

        private void BtnMoveUp_Click(object? sender, RoutedEventArgs e)
        {
            MoveDependency(true);
        }

        private void BtnMoveDown_Click(object? sender, RoutedEventArgs e)
        {
            MoveDependency(false);
        }

        private async void MoveDependency(bool moveUp)
        {
            AssetsFileInstance? selectedFile = activeFile;
            if (allMode)
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "您必须从文件中移动依赖项，而不是整个工作区。");

                return;
            }

            DependencyListBoxItem? dependency = GetSelectedDependency();
            if (dependency == null)
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "您必须选择要移动的依赖项。");

                return;
            }

            if (!dependency.isDependency || (moveUp && boxDependenciesList.SelectedIndex == 1))
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "您不能移动基础文件。");

                return;
            }

            List<AssetsFileExternal> deps = dependencyMap[selectedFile];

            if (!moveUp && boxDependenciesList.SelectedIndex == deps.Count)
            {
                await MessageBoxUtil.ShowDialog(this, "错误",
                    "您不能再向下移动。");

                return;
            }

            int originalDependencyCount = selectedFile.file.Metadata.Externals.Count;
            int moveUpCheckOffset = moveUp ? 1 : 0; // if moving up, don't allow moving the next item either
            if (!askedAboutMoving && boxDependenciesList.SelectedIndex <= originalDependencyCount + moveUpCheckOffset)
            {
                bool shouldContinue = await ShowMoveConfirmationDialog();
                if (!shouldContinue)
                    return;

                askedAboutMoving = true;
            }

            AssetsFileExternal dep = dependency.dependency!;
            int depIndex = deps.IndexOf(dep);
            int moveUpOffset = moveUp ? -1 : 1;

            dependencyMap[selectedFile].RemoveAt(depIndex);
            dependencyMap[selectedFile].Insert(depIndex + moveUpOffset, dep);
            dependenciesModified.Add(selectedFile);

            UpdateDepsListBox();
        }

        public void HandleDepsSaving(Dictionary<AssetsFileInstance, AssetsFileChangeTypes> changedFilesList)
        {
            foreach (AssetsFileInstance file in dependenciesModified)
            {
                List<AssetsFileExternal> deps = dependencyMap[file];
                file.file.Metadata.Externals = deps;

                if (!changedFilesList.ContainsKey(file))
                    changedFilesList[file] = AssetsFileChangeTypes.Dependencies;
                else
                    changedFilesList[file] |= AssetsFileChangeTypes.Dependencies;
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            btnAdd.IsEnabled = enabled;
            btnEdit.IsEnabled = enabled;
            btnRemove.IsEnabled = enabled;
            btnMoveUp.IsEnabled = enabled;
            btnMoveDown.IsEnabled = enabled;
        }

        private DependencyListBoxItem? GetSelectedDependency()
        {
            return (DependencyListBoxItem?)boxDependenciesList.SelectedItem;
        }

        private async Task<bool> ShowMoveConfirmationDialog()
        {
            var result = await MessageBoxUtil.ShowDialog(this, "警告",
                "您确定要移动此依赖项吗？此功能不会自动重新映射此文件中的资源文件标识。只有在您知道自己在做什么时才使用此功能。", MessageBoxType.YesNo);

            return result == MessageBoxResult.Yes;
        }

        private class DependencyComboBoxItem
        {
            public string text;
            public AssetsFileInstance? file;

            public DependencyComboBoxItem(string text, AssetsFileInstance? file)
            {
                this.text = text;
                this.file = file;
            }

            public override string ToString()
            {
                return text;
            }
        }

        private class DependencyListBoxItem
        {
            public int index;
            public AssetsFileInstance? file;
            public AssetsFileExternal? dependency;
            public bool isDependency;

            public DependencyListBoxItem(int index, AssetsFileInstance? file)
            {
                this.index = index;
                this.file = file;
                isDependency = false;
            }

            public DependencyListBoxItem(int index, AssetsFileExternal? dependency)
            {
                this.index = index;
                this.dependency = dependency;
                isDependency = true;
            }

            public override string ToString()
            {
                if (isDependency && dependency != null)
                {
                    if (dependency.PathName != string.Empty)
                        return $"{index} - {dependency.PathName}";
                    else
                        return $"{index} - {dependency.Guid}";
                }
                else if (!isDependency && file != null)
                    return $"{index} - {file.name}";
                else
                    return $"{index} - ???";
            }
        }
    }
}
