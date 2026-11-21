using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using mcpx_template_project.Domain.VO;
using mcpx_template_project.Service.Admin;
using mcpx_template_project.Service.Business.CommSetting;
using mcpx_template_project.Service.Business.Plc;
using mcpx_template_project.Service.Business.Recipe;
using RecipeEntity = mcpx_template_project.Domain.Entities.Business.Recipe.Recipe;

namespace mcpx_template_project.ViewModels.Business.EquipmentInfo.PNSelect
{
    /// <summary>
    /// PNSelect（料号/配方选择）页面视图模型。
    /// 新增/编辑/删除只操作数据库（通过 <see cref="RecipeEditViewModel"/> 弹窗），
    /// 仅「写入配方」触发 PLC 写入（通过通用 <see cref="IPlcObjectService"/>）；
    /// 右侧实时读回 PLC 实际值。
    /// </summary>
    /// <author>Rudeus</author>
    public partial class PagePnSelectViewModel : ObservableObject
    {
        private readonly IRecipeService _recipeService;
        private readonly IPlcObjectService _plcObjectService;
        private readonly IPlcCommunicationService _plcService;
        private readonly ICommSettingService _commSettingService;
        private readonly IDialogService _dialogService;
        private readonly IViewModelFactory _viewModelFactory;
        private readonly DispatcherTimer _timer;

        // ============ 列表 ============

        public ObservableCollection<RecipeEntity> Recipes { get; } = new();

        [ObservableProperty] private string _keyword = string.Empty;

        [ObservableProperty] private string _summary = string.Empty;

        [ObservableProperty] private RecipeEntity? _selectedRecipe;

        // ============ 写入 PLC ============

        public ObservableCollection<CommSettingVO> ConnectedSettings { get; } = new();

        [ObservableProperty] private CommSettingVO? _selectedSetting;

        [ObservableProperty] private string _writeStatus = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanWrite))]
        private bool _isWriting;

        /// <summary>是否可执行写入（IsWriting 取反）</summary>
        public bool CanWrite => !IsWriting && SelectedRecipe != null && SelectedSetting != null;

        /// <summary>写入结果三态：null=未写入，true=成功，false=失败（驱动圆点颜色）</summary>
        [ObservableProperty] private bool? _writeSuccessFlag;

        /// <summary>写入结果文字（未写入 / 配方「XXX」写入成功 / 写入失败）</summary>
        [ObservableProperty] private string _writeSuccessText = "未写入配方";

        // ============ 实时读回 ============

        /// <summary>目标配方（选中/写入的配方，作为读回对比的期望值）</summary>
        [ObservableProperty] private RecipeEntity? _targetRecipe;

        /// <summary>当前读回配方（PLC 读回的实时值）</summary>
        [ObservableProperty] private RecipeEntity? _currentRecipe;

        public PagePnSelectViewModel(
            IRecipeService recipeService,
            IPlcObjectService plcObjectService,
            IPlcCommunicationService plcService,
            ICommSettingService commSettingService,
            IDialogService dialogService,
            IViewModelFactory viewModelFactory)
        {
            _recipeService = recipeService;
            _plcObjectService = plcObjectService;
            _plcService = plcService;
            _commSettingService = commSettingService;
            _dialogService = dialogService;
            _viewModelFactory = viewModelFactory;

            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;

            // 打开界面即开始轮询读回（无需写入后才启动）；目标值在选中配方时填充。
            _timer.Start();

            _ = LoadAsync();
        }

        // ============ 列表加载 ============

        [RelayCommand]
        public async Task LoadAsync()
        {
            try
            {
                var list = await _recipeService.QueryAllAsync(Keyword);
                Recipes.Clear();
                foreach (var item in list)
                    Recipes.Add(item);
                Summary = $"共 {list.Count} 条配方";

                await LoadConnectedSettingsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("加载配方失败：" + ex.Message);
            }
        }

        /// <summary>加载已连接的通讯设定列表（写入目标 PLC）</summary>
        private async Task LoadConnectedSettingsAsync()
        {
            try
            {
                var connectedIds = _plcService.GetConnectedSettingIds().ToHashSet();
                var all = await _commSettingService.QueryAllAsync();

                ConnectedSettings.Clear();
                foreach (var s in all.Where(s => connectedIds.Contains(s.Id)))
                    ConnectedSettings.Add(s);

                if (SelectedSetting == null || !connectedIds.Contains(SelectedSetting.Id))
                    SelectedSetting = ConnectedSettings.FirstOrDefault();
            }
            catch
            {
                // 忽略通讯设定加载失败，不阻断配方列表展示
            }
        }

        #region 配方数据库操作
        /// <summary>选中配方时，作为读回对比的目标值</summary>
        partial void OnSelectedRecipeChanged(RecipeEntity? value)
        {
            TargetRecipe = value;
            OnPropertyChanged(nameof(CanWrite));
        }

        /// <summary>选中目标 PLC 时刷 新写入按钮可用性</summary>
        partial void OnSelectedSettingChanged(CommSettingVO? value)
        {
            OnPropertyChanged(nameof(CanWrite));
        }

        /// <summary>打开新增配方弹窗</summary>
        [RelayCommand]
        private void AddRecipe()
        {
            var editVm = _viewModelFactory.Create<RecipeEditViewModel>();
            OpenEditWindow(editVm);
        }

        /// <summary>打开编辑配方弹窗</summary>
        [RelayCommand]
        private void EditRecipe(RecipeEntity? recipe)
        {
            if (recipe == null) return;
            var editVm = _viewModelFactory.Create<RecipeEditViewModel>(recipe);
            OpenEditWindow(editVm);
        }

        private async void OpenEditWindow(RecipeEditViewModel editVm)
        {
            var window = new Views.Pages.Business.EquipmentInfo.PNSelect.RecipeEditWindow(editVm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (window.ShowDialog() == true)
            {
                var saved = await editVm.SaveAsync();
                if (saved != null)
                {
                    await LoadAsync();
                    SelectedRecipe = Recipes.FirstOrDefault(r => r.Id == saved.Id);
                }
            }
        }

        [RelayCommand]
        private async Task DeleteRecipe(RecipeEntity? recipe)
        {
            if (recipe == null) return;

            try
            {
                if (!_dialogService.Confirm($"确定删除配方 \"{recipe.RecipeName}\" 吗？", "确认删除"))
                    return;

                await _recipeService.DeleteAsync(recipe.Id);
                _dialogService.ShowSuccess("删除成功");

                if (SelectedRecipe?.Id == recipe.Id)
                    SelectedRecipe = null;

                await LoadAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("删除失败：" + ex.Message);
            }
        }
        #endregion


        #region 读写PLC
        [RelayCommand]
        private async Task WriteToPlcAsync()
        {
            if (SelectedRecipe == null)
            {
                _dialogService.ShowWarning("请先在左侧选择一个配方");
                return;
            }

            if (SelectedSetting == null)
            {
                _dialogService.ShowWarning("请先在「通讯设定」中启动连线，并选择一个已连接的目标 PLC");
                return;
            }

            var recipe = SelectedRecipe;

            IsWriting = true;
            WriteStatus = $"正在写入配方 \"{recipe.RecipeName}\" 到 {SelectedSetting.Name} ...";

            try
            {
                var result = await _plcObjectService.WriteAsync<RecipeEntity>(SelectedSetting.Id, recipe);
                WriteStatus = result.Message;
                WriteSuccessFlag = result.Success;
                // 成功文案使用左侧列表当前选中的配方名（而非本次局部变量）
                var name = SelectedRecipe?.RecipeName ?? recipe.RecipeName;
                WriteSuccessText = result.Success
                    ? $"配方「{name}」写入成功"
                    : $"配方「{name}」写入失败";

                if (result.Success)
                    _dialogService.ShowSuccess(result.Message);
                else
                    _dialogService.ShowError(result.Message);

                // 写入后无需手动读回：定时器一直在轮询，读回值会自动刷新。
                // 目标值即本次写入的配方（TargetRecipe 已由选中配方同步）。
                TargetRecipe = recipe;
            }
            catch (Exception ex)
            {
                WriteStatus = "写入异常：" + ex.Message;
                WriteSuccessFlag = false;
                var name = SelectedRecipe?.RecipeName ?? recipe.RecipeName;
                WriteSuccessText = $"配方「{name}」写入失败";
                _dialogService.ShowError("写入失败：" + ex.Message);
            }
            finally
            {
                IsWriting = false;
            }
        }

        [RelayCommand]
        private void RefreshConnections() => _ = LoadConnectedSettingsAsync();

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (SelectedSetting == null)
                return;

            // 从缓存读回强类型配方对象，替换绑定对象
            CurrentRecipe = _plcObjectService.Read<RecipeEntity>(SelectedSetting.Id);
        }
        #endregion 
    }
}
