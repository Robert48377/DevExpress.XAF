//using DevExpress.Xpo;
//using Xpand.XAF.Persistent.BaseImpl;
//
//namespace Xpand.XAF.Agnostic.Tests.Modules.MasterDetail.BOModel{
//    public class ChildMd:CustomBaseObject{
//        public ChildMd(Session session) : base(session){
//        }
//
//        string _primitiveProperty;
//
//        public string PrimitiveProperty{
//            get => _primitiveProperty;
//            set => SetPropertyValue(nameof(PrimitiveProperty), ref _primitiveProperty, value);
//        }
//
//        Md _parentMd;
//
//        [Association("Md-ChildMds")]
//        public Md Md{
//            get => _parentMd;
//            set => SetPropertyValue(nameof(Md), ref _parentMd, value);
//        }
//    }
//}