// 全局 using：把每个文件都要写的命名空间集中到这里，业务文件保持干净。
// 注意：WinUI 的类型（Window/Frame/Page 等）不在 SDK 隐式 using 范围内，必须显式引入。

global using System;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

global using CommunityToolkit.Mvvm.ComponentModel;
global using CommunityToolkit.Mvvm.Input;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
