﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Editors;
using Fasterflect;
using Ryder;
using Xpand.XAF.Modules.Reactive.Extensions;
using Xpand.XAF.Modules.Reactive.Services;

namespace Xpand.XAF.Modules.Reactive{

    public static class RxApp{
        static readonly ISubject<XafApplication> ApplicationSubject=Subject.Synchronize(new BehaviorSubject<XafApplication>(null));
        private static readonly MethodInvoker CreateWindowCore;
        private static readonly IObservable<RedirectionContext> OnPopupWindowCreated;
        private static readonly MethodInvoker CreateControllersOptimized;
        private static readonly MethodInvoker CreateControllers;
        private static readonly IObservable<RedirectionContext> NestedFrameRedirection;
        private static readonly IObservable<RedirectionContext> CreateWindow;
        private static readonly MemberGetter ViewApplication;


        internal static IObservable<XafApplication> Connect(this XafApplication application){
            return application.AsObservable()
                .Do(_ => ApplicationSubject.OnNext(_))
                .Select(xafApplication => xafApplication);
        }

        static RxApp(){
            ViewApplication = typeof(CompositeView).Property("Application").DelegateForGetPropertyValue();
            var methodInfos = typeof(XafApplication).Methods();
            CreateControllersOptimized = methodInfos.Where(info => info.Name==nameof(CreateControllers)&&info.Parameters().Count==4).Select(info => info.DelegateForCallMethod()).First();
            CreateControllers = methodInfos.Where(info => info.Name==nameof(CreateControllers)&&info.Parameters().Count==3).Select(info => info.DelegateForCallMethod()).First();
            CreateWindowCore = methodInfos.First(info => info.Name == nameof(CreateWindowCore)).DelegateForCallMethod();
            CreateWindow = Redirection.Observe(methodInfos.First(info => info.Name==nameof(XafApplication.CreateWindow)))
                .Select(context => context).Publish().RefCount();
            
            OnPopupWindowCreated = Redirection.Observe(methodInfos.First(info => info.Name==nameof(OnPopupWindowCreated)))
                .Publish().RefCount();

            var createNestedFrame = methodInfos.First(info => info.Name==nameof(XafApplication.CreateNestedFrame));
            NestedFrameRedirection = Redirection.Observe(createNestedFrame)
                .Publish().RefCount();
        }

        public static IObservable<Unit> UpdateMainWindowStatus<T>(IObservable<T> messages,TimeSpan period=default){
            if (period==default)
                period=TimeSpan.FromSeconds(5);
            return WindowTemplateService.UpdateStatus(period, messages);
        }

        public static IObservable<Window> Windows{
            get{
                return CreateWindow
                    .Select(context => {
                        var templateContext = (TemplateContext) context.Arguments[0];
                        var controllers = (ICollection<Controller>) context.Arguments[1];
                        var createAllControllers = (bool) context.Arguments[2];
                        var isMain = (bool) context.Arguments[3];
                        var view = (View) context.Arguments[4];
                        var application = (XafApplication) context.Sender;

                        var list = application.OptimizedControllersCreation
                            ? CreateControllers(application,typeof(Controller), createAllControllers, controllers, view)
                            : CreateControllers(application, typeof(Controller),createAllControllers, controllers);
                        var window = (Window) CreateWindowCore(application,templateContext, list, isMain, true);
                        context.ReturnValue = window;
                        return window;
                    });
            }
        }

        public static IObservable<Frame> NestedFrames{
            get{
                return NestedFrameRedirection
                    .Select(context => {
                        var viewItem = (ViewItem) context.Arguments[0];
                        var application = (XafApplication)viewItem.View.GetPropertyValue("Application");
                        var controllers = application.OptimizedControllersCreation
                            ? (ICollection<Controller>) CreateControllersOptimized.Invoke(application,
                                typeof(Controller), true, (ICollection<Controller>) null,
                                (View) context.Arguments[2])
                            : (ICollection<Controller>) CreateControllers.Invoke(application, typeof(Controller),
                                true, (ICollection<Controller>) null);
                        var nestedFrame = new NestedFrame(application, (TemplateContext) context.Arguments[1], viewItem, controllers);
                        context.ReturnValue = nestedFrame;
                        return nestedFrame;
                    });
            }
        }

        public static IObservable<XafApplication> Application => ApplicationSubject.WhenNotDefault();

        public static IObservable<Window> MainWindow => throw new NotImplementedException();

        public static IObservable<(Frame masterFrame, NestedFrame detailFrame)> MasterDetailFrames(Type masterType, Type childType){
            throw new NotImplementedException();
//            var nestedlListViews = Windows(ViewType.ListView, childType, Nesting.Nested)
//                .Select(_ => _)
//                .Cast<NestedFrame>();
//            return Windows(ViewType.DetailView, masterType)
//                .CombineLatest(nestedlListViews.WhenIsNotOnLookupPopupTemplate(),
//                    (masterFrame, detailFrame) => (masterFrame, detailFrame))
//                .TakeUntilDisposingMainWindow();
        }

        public static IObservable<(Frame masterFrame, NestedFrame detailFrame)> NestedDetailObjectChanged(Type nestedType, Type childType){
            return MasterDetailFrames(nestedType, childType).SelectMany(_ => {
                return _.masterFrame.View.WhenCurrentObjectChanged().Select(tuple => _);
            });
        }

        public static IObservable<(ObjectsGettingEventArgs e, TSignal signals, Frame masterFrame, NestedFrame detailFrame)> AddNestedNonPersistentObjects<TSignal>(Type masterObjectType, Type detailObjectType,
                Func<(Frame masterFrame, NestedFrame detailFrame), IObservable<TSignal>> addSignal){

            return Observable.Create<(ObjectsGettingEventArgs e,TSignal signals, Frame masterFrame, NestedFrame detailFrame)>(
                observer => {
                    return NestedDetailObjectChanged(masterObjectType, detailObjectType)
                        .SelectMany(_ => AddNestedNonPersistentObjectsCore(addSignal, _, observer))
                        .Subscribe(response => {},() => {});
                });
        }

        public static IObservable<(ObjectsGettingEventArgs e, TSignal signal, Frame masterFrame, NestedFrame detailFrame)> AddNestedNonPersistentObjects<TSignal>(this IObservable<(Frame masterFrame, NestedFrame detailFrame)> source,
                Func<(Frame masterFrame, NestedFrame detailFrame), IObservable<TSignal>> addSignal){

            return source.SelectMany(tuple => {
                return Observable.Create<(ObjectsGettingEventArgs e,TSignal signal, Frame masterFrame, NestedFrame detailFrame)>(
                    observer => AddNestedNonPersistentObjectsCore(addSignal, tuple, observer).Subscribe());

            });
        }

        private static IObservable<TSignal> AddNestedNonPersistentObjectsCore<TSignal>(Func<(Frame masterFrame, NestedFrame detailFrame), IObservable<TSignal>> addSignal,
            (Frame masterFrame, NestedFrame detailFrame) _, IObserver<(ObjectsGettingEventArgs e, TSignal signal, Frame masterFrame, NestedFrame detailFrame)> observer){
            return addSignal(_)
                .When(_.masterFrame, _.detailFrame)
                .Select(signals => {
                    using (var unused = ((NonPersistentObjectSpace) _.detailFrame.View.ObjectSpace)
                        .WhenObjectsGetting()
                        .Do(tuple => observer.OnNext((tuple.e, signals, _.masterFrame, _.detailFrame)),() => {})
                        .Subscribe()){
                        ((ListView) _.detailFrame.View).CollectionSource.ResetCollection();
                    }

                    return signals;
                });
        }
    }

}