using LwWinApp.Models.Dtos;
using LwWinApp.ViewModels;

namespace LwWinApp.Views.System;

/// <summary>部门架构页。</summary>
public sealed partial class DeptTreePage : PageBase
{
    public DeptTreePage()
    {
        InitializeComponent();
        Vm = Require<DeptTreeViewModel>();
    }

    public DeptTreeViewModel Vm { get; }

    public override INavigationAware? ViewModel => Vm;

    /// <summary>
    /// 用户点节点。用 ItemInvoked 而不是 SelectedItem 双向绑定 ——
    /// TreeView.SelectedItem 在整棵树被替换（每次增删改都重建）之后会丢，
    /// 事件驱动反而更直接。
    /// </summary>
    private void OnTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        // InvokedItem 是模板里的数据项。点到折叠箭头时它是 TreeViewNode 而不是 DeptNode，
        // 所以要判断一下类型，不能硬转。
        var node = args.InvokedItem switch
        {
            DeptNode dept => dept,
            TreeViewNode { Content: DeptNode dept } => dept,
            _ => null
        };

        Vm.SelectNode(node);
    }
}
