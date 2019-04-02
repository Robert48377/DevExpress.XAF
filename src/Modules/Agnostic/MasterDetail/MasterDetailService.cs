using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Persistent.Base;
using Xpand.Source.Extensions.XAF.ApplicationModulesManager;
using Xpand.Source.Extensions.XAF.Frame;
using Xpand.XAF.Modules.Reactive;
using Xpand.XAF.Modules.Reactive.Extensions;
using Xpand.XAF.Modules.Reactive.Services;
using Xpand.XAF.Modules.Reactive.Services.Controllers;

namespace Xpand.XAF.Modules.MasterDetail{
    public static class MasterDetailService{
        public const string MasterDetailSaveAction = "MasterDetailSaveAction";

        internal static IObservable<Unit> Connect(this ApplicationModulesManager applicationModulesManager){
            var connect = MasterDetailDashboardViewItems
                .SelectMany(_ =>ChangeCurrentObject((ListView) _.listViewItem.InnerView, (DetailView) _.detailViewItem.InnerView).ToUnit())
                .Merge(ListViewProcessSelectedItem.ToUnit())
                .Merge(DisableListViewController("ListViewFastCallbackHandlerController"))
                .Merge(DisableDetailViewViewController("ActionsFastCallbackHandlerController"))
                .Merge(SaveAction)
                .Merge(RefreshListView)
                .ToUnit();
            return applicationModulesManager.RegisterActions().ToUnit()
                .Concat(connect)
                .TakeUntilDisposingMainWindow();
        }

        private static IObservable<Unit> RefreshListView{
            get{
                return MasterDetailDashboardViewItems
                    .SelectMany(_ => _.detailViewItem.InnerView.ObjectSpace
                        .WhenCommited()
                        .Do(o => _.listViewItem.InnerView.ObjectSpace.Refresh()))
                    .ToUnit();
            }
        }

        private static IObservable<Unit> SaveAction{
            get{
                return MasterDetailDashboardViewItems
                    .Do(_ => _.detailViewItem.Frame.Actions().First(action => action.Id == MasterDetailSaveAction).Active[MasterDetailModule.CategoryName] = true)
                    .Select(_ => _.detailViewItem.Frame.Actions<SimpleAction>().Where(action => action.Id==MasterDetailSaveAction)
                        .Select(action => action.WhenExecuted()).Merge()
                        .Do(tuple => {
                            tuple.objectSpace.CommitChanges();
                        }))
                    .Merge().ToUnit();
            }
        }

        public static IObservable<(DashboardViewItem listViewItem, DashboardViewItem detailViewItem)> MasterDetailDashboardViewItems{get;}=RxApp.Application
            .Select(application => application)
            .DashboardViewCreated().Where(_ => ((IModelDashboardViewMasterDetail) _.e.View.Model).MasterDetail)
            .SelectMany(_ => _.e.View.WhenControlsCreated())
            .SelectMany(_ => _.view.GetItems<DashboardViewItem>().Where(item => item.Model.View is IModelListView)
                .SelectMany(listViewItem => _.view
                    .GetItems<DashboardViewItem>().Where(viewItem =>viewItem.Model.View is IModelDetailView && viewItem.Model.View.AsObjectView.ModelClass ==listViewItem.Model.View.AsObjectView.ModelClass)
                    .Select(detailViewItem => (listViewItem, detailViewItem))
                )
            );

        public static IObservable<(ListViewProcessCurrentObjectController controller, CustomProcessListViewSelectedItemEventArgs e)> ListViewProcessSelectedItem{ get;} =MasterDetailDashboardViewItems
            .SelectMany(_ => _.listViewItem.Frame
                .GetController<ListViewProcessCurrentObjectController>()
                .WhenCustomProcessSelectedItem())
            .Do(_ => _.e.Handled = true);

        public static IObservable<object> ChangeCurrentObject(ListView listView, DetailView detailView){
            return listView.WhenSelectionChanged()
                .Select(_ => listView.SelectedObjects.Cast<object>().FirstOrDefault())
                .DistinctUntilChanged()
                .Select(o => {
                    detailView.CurrentObject = detailView.ObjectSpace.GetObject(o);
                    return detailView.CurrentObject;
                });
        }

        static IObservable<ActionBase> RegisterActions(this ApplicationModulesManager applicationModulesManager){
            return applicationModulesManager.RegisterViewAction(MasterDetailSaveAction, _ => {
                var simpleAction =
                    new SimpleAction(_.controller, _.id, PredefinedCategory.Edit.ToString()){
                        Caption = "Save",
                        ImageName = "MenuBar_Save",
                        TargetViewType = ViewType.DetailView
                    };
                simpleAction.Active[MasterDetailModule.CategoryName] = false;
                return simpleAction;
            }).AsObservable().FirstAsync();
        }

        static IObservable<Unit> DisableListViewController(string typeName){
            return MasterDetailDashboardViewItems
                .SelectMany(_ => _.listViewItem.Frame.Controllers.Cast<Controller>().Where(controller => controller.GetType().Name==typeName))
                .Do(controller => controller.Active[MasterDetailModule.CategoryName]=false).ToUnit();
        }
        static IObservable<Unit> DisableDetailViewViewController(string typeName){
            return MasterDetailDashboardViewItems
                .SelectMany(_ => _.detailViewItem.Frame.Controllers.Cast<Controller>().Where(controller => controller.GetType().Name==typeName))
                .Do(controller => controller.Active[MasterDetailModule.CategoryName]=false).ToUnit();
        }
    }
}