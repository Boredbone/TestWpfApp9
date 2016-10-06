using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

namespace WpfApplication9
{
    class MainWindowViewModel : IDisposable
    {
        private readonly CompositeDisposable disposables;

        public ReadOnlyReactiveProperty<int> MaxWorkerThreads { get; }
        public ReadOnlyReactiveProperty<int> AvailableWorkerThreads { get; }
        public ReadOnlyReactiveProperty<int> BusyWorkerThreads { get; }
        public ReadOnlyReactiveProperty<int> MinWorkerThreads { get; }
        public ReadOnlyReactiveProperty<int> MaxCompletionPortThreads { get; }
        public ReadOnlyReactiveProperty<int> AvailableCompletionPortThreads { get; }
        public ReadOnlyReactiveProperty<int> BusyCompletionPortThreads { get; }
        public ReadOnlyReactiveProperty<int> MinCompletionPortThreads { get; }

        public ReactiveProperty<int> Count { get; }

        public ReactiveCommand HeavyWorkCommand1 { get; }
        public ReactiveCommand HeavyWorkCommand2 { get; }
        public ReactiveCommand HeavyWorkCommand3 { get; }

        public ObservableCollection<string> Log { get; }

        public MainWindowViewModel()
        {
            this.disposables = new CompositeDisposable();

            this.Log = new ObservableCollection<string>();

            this.Count = new ReactiveProperty<int>(8).AddTo(this.disposables);

            //ThreadPool.SetMinThreads(100, 100);

            #region ThreadPoolStatus

            var maxThreads = ObserveEveryValueChangedExtensions.ObserveEveryValueChanged(this, _ =>
            {
                int workerThreads;
                int completionPortThreads;
                ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);
                return new ThreadPoolStatus(workerThreads, completionPortThreads);
            })
            .Publish().RefCount();


            var availableThreads = ObserveEveryValueChangedExtensions.ObserveEveryValueChanged(this, _ =>
            {
                int workerThreads;
                int completionPortThreads;
                ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);
                return new ThreadPoolStatus(workerThreads, completionPortThreads);
            })
            .Publish().RefCount();

            var minThreads = ObserveEveryValueChangedExtensions.ObserveEveryValueChanged(this, _ =>
            {
                int workerThreads;
                int completionPortThreads;
                ThreadPool.GetMinThreads(out workerThreads, out completionPortThreads);
                return new ThreadPoolStatus(workerThreads, completionPortThreads);
            })
            .Publish().RefCount();



            this.MaxWorkerThreads = maxThreads.Select(x => x.WorkerThreads)
                .ToReadOnlyReactiveProperty().AddTo(this.disposables);

            this.AvailableWorkerThreads = availableThreads.Select(x => x.WorkerThreads)
                .ToReadOnlyReactiveProperty().AddTo(this.disposables);

            this.BusyWorkerThreads = this.MaxWorkerThreads
                .CombineLatest(this.AvailableWorkerThreads, (max, available) => max - available)
                .ToReadOnlyReactiveProperty().AddTo(this.disposables);

            this.MinWorkerThreads = minThreads.Select(x => x.WorkerThreads)
                .ToReadOnlyReactiveProperty().AddTo(this.disposables);


            this.MaxCompletionPortThreads = maxThreads.Select(x => x.CompletionPortThreads)
                .ToReadOnlyReactiveProperty().AddTo(this.disposables);

            this.AvailableCompletionPortThreads = availableThreads.Select(x => x.CompletionPortThreads)
                .ToReadOnlyReactiveProperty().AddTo(this.disposables);

            this.BusyCompletionPortThreads = this.MaxCompletionPortThreads
                .CombineLatest(this.AvailableCompletionPortThreads, (max, available) => max - available)
                .ToReadOnlyReactiveProperty().AddTo(this.disposables);

            this.MinCompletionPortThreads = minThreads.Select(x => x.CompletionPortThreads)
                .ToReadOnlyReactiveProperty().AddTo(this.disposables);

            #endregion

            // asynchronous without thread pool
            this.HeavyWorkCommand1 = new ReactiveCommand().AddTo(this.disposables);
            this.HeavyWorkCommand1.Subscribe(async _ =>
            {
                using (new TestContainer("1", this.Log))
                {
                    await Task.WhenAll(Enumerable.Range(0, this.Count.Value).Select(c => Task.Delay(2000)));
                }
            })
            .AddTo(this.disposables);


            // asynchronous with thread pool
            this.HeavyWorkCommand2 = new ReactiveCommand().AddTo(this.disposables);
            this.HeavyWorkCommand2.Subscribe(async _ =>
            {
                using (new TestContainer("2", this.Log))
                {
                    await Task.WhenAll(Enumerable.Range(0, this.Count.Value).Select(c => Task.Run(() => Thread.Sleep(2000))));
                }
            })
            .AddTo(this.disposables);


            // synchronous on UI thread
            this.HeavyWorkCommand3 = new ReactiveCommand().AddTo(this.disposables);
            this.HeavyWorkCommand3.Subscribe(_ =>
            {
                using (new TestContainer("3", this.Log))
                {
                    Task.Delay(2000).Wait();
                }
            })
            .AddTo(this.disposables);
        }

        public void Dispose() => this.disposables.Dispose();

        private class TestContainer : IDisposable
        {
            public string Name { get; set; }
            private readonly Stopwatch watch;
            public ObservableCollection<string> Log { get; }

            public TestContainer(string name, ObservableCollection<string> log)
            {
                this.Name = name;

                this.Log = log;

                this.watch = new Stopwatch();
                this.Log.Add($"{this.Name} start");
                this.watch.Start();
            }

            public void Dispose()
            {
                this.watch.Stop();
                this.Log.Add($"{this.Name} end {this.watch.ElapsedMilliseconds} ms");
            }
        }

        private class ThreadPoolStatus
        {
            public int WorkerThreads { get; }
            public int CompletionPortThreads { get; }

            public ThreadPoolStatus(int workerThreads, int completionPortThreads)
            {
                this.WorkerThreads = workerThreads;
                this.CompletionPortThreads = completionPortThreads;
            }
        }
    }
}
