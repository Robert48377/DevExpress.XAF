using System.ComponentModel;
using System.Linq;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Model.Core;
using DevExpress.Persistent.Base;

namespace Xpand.XAF.Modules.MasterDetail{
    public interface IModelApplicationMasterDetail{
        IModelDashboardMasterDetail DashboardMasterDetail{ get; }
    }

    public interface IModelDashboardMasterDetail:IModelNode{
        IModelMasterDetailDetailViewObjectTypeLinks ObjectTypeLinks{ get; }
    }

    [ModelNodesGenerator(typeof(MasterDetailViewObjectTypeLinkNodesGenerator))]
    public interface IModelMasterDetailDetailViewObjectTypeLinks : IModelNode, IModelList<IModelMasterDetailViewObjectTypeLink> {
    }

    public interface IModelMasterDetailViewObjectTypeLink:IModelNode{
        [Required]
        [DataSourceProperty("Application.DetailViews")]
        IModelDetailView DetailView { get; set; }
        [DataSourceProperty("Application.BOModel")]
        [Required]
        IModelClass ModelClass { get; set; }

        string Criteria{ get; set; }
    }

    public class MasterDetailViewObjectTypeLinkNodesGenerator : ModelNodesGeneratorBase {
        protected override void GenerateNodesCore(ModelNode node) {

        }
    }

    [ModelAbstractClass]
    public interface IModelDashboardViewMasterDetail : IModelDashboardView {
        [Category(MasterDetailModule.CategoryName)]
        [ModelBrowsable(typeof(ModelDashboardViewMasterDetailVisibilityCalculator))]
        bool MasterDetail { get; set; }
        [Category(MasterDetailModule.CategoryName)]
        [ModelBrowsable(typeof(ModelDashboardViewMasterDetailVisibilityCalculator))]
        IModelMasterDetailDetailViewObjectTypeLinks MasterDetailDetailViewObjectTypeLinks { get; }
    }

    [DomainLogic(typeof(IModelDashboardViewMasterDetail))]
    public class ModelDashboardViewMasterDetailDomainLogic {
        public static bool Get_MasterDetail(IModelDashboardViewMasterDetail dashboardViewMasterDetail) {
            return new ModelDashboardViewMasterDetailVisibilityCalculator().IsVisible(dashboardViewMasterDetail, null);
        }
    }

    public class ModelDashboardViewMasterDetailVisibilityCalculator : IModelIsVisible {
        public bool IsVisible(IModelNode node, string propertyName) {
            if (propertyName==nameof(IModelDashboardViewMasterDetail.MasterDetail)){
                var viewItems = ((IModelDashboardViewMasterDetail)node).Items.OfType<IModelDashboardViewItem>().ToArray();
                var modelObjectViews = viewItems.Select(item => item.View).OfType<IModelObjectView>().ToArray();
                return modelObjectViews.Length == 2 && modelObjectViews.Length == viewItems.Length &&
                       modelObjectViews.GroupBy(view => view.ModelClass).Count() == 1&&modelObjectViews.First().GetType()!=modelObjectViews.Last().GetType();
            }

            return ((IModelDashboardViewMasterDetail) node).MasterDetail;
        }
    }
}