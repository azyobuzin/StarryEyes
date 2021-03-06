﻿using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using StarryEyes.SweetLady.DataModel;
using StarryEyes.Vanille.DataStore;
using StarryEyes.Vanille.DataStore.Persistent;
using System.Threading;
using System.Threading.Tasks;
using StarryEyes.Mystique.Models.Hub;

namespace StarryEyes.Mystique.Models.Store
{
    /// <summary>
    /// Storage for twitter statuses.
    /// </summary>
    public static class StatusStore
    {
        #region publish block

        private static Subject<StatusNotification> statusPublisher = new Subject<StatusNotification>();

        public static IObservable<StatusNotification> StatusPublisher
        {
            get { return statusPublisher; }
        }

        #endregion

        private static bool _isInShutdown = false;

        private static DataStoreBase<long, TwitterStatus> store;

        public static void Initialize()
        {
            // initialize
            if (StoreOnMemoryObjectPersistence.IsPersistentDataExisted("statuses"))
            {
                try
                {
                    store = new PersistentDataStore<long, TwitterStatus>
                        (_ => _.Id, Path.Combine(App.DataStorePath, "statuses"),
                        tocniops: StoreOnMemoryObjectPersistence.GetPersistentData("statuses"));
                }
                catch (Exception ex)
                {
                    InformationHub.PublishInformation(new Information(InformationKind.Warning,
                        "STATUSSTORE_INIT_FAILED",
                        "ステータス データベースが破損しています。",
                        "読み込み時にエラーが発生したため、データベースを初期化しました。" + Environment.NewLine +
                        "送出された例外: " + ex.ToString()));
                }
            }
            if (store == null)
            {
                store = new PersistentDataStore<long, TwitterStatus>
                    (_ => _.Id, Path.Combine(App.DataStorePath, "statuses"));
            }
            App.OnApplicationFinalize += Shutdown;
        }

        /// <summary>
        /// Get stored status counts.<para />
        /// If you want this param, please consider using StatisticsHub instead of this.
        /// </summary>
        public static int Count
        {
            get { return store.Count; }
        }

        /// <summary>
        /// Store a tweet.
        /// </summary>
        /// <param name="status">storing status</param>
        /// <param name="publish">flag of publish status for other listening children</param>
        public static void Store(TwitterStatus status, bool publish = true)
        {
            if (_isInShutdown) return;
            if (publish)
                statusPublisher.OnNext(new StatusNotification()
                {
                    IsAdded = true,
                    Status = status,
                    StatusId = status.Id
                });
            store.Store(status);
            UserStore.Store(status.User);
        }

        /// <summary>
        /// Get tweet.
        /// </summary>
        /// <param name="id">find id</param>
        /// <returns>contains a tweet or empty observable.</returns>
        public static IObservable<TwitterStatus> Get(long id)
        {
            if (_isInShutdown) return Observable.Empty<TwitterStatus>();
            return store.Get(id)
                .Do(_ => Store(_, false)); // add to local cache
        }

        /// <summary>
        /// Find tweets.
        /// </summary>
        /// <param name="predicate">find predicate</param>
        /// <param name="range">finding range</param>
        /// <returns>results observable sequence.</returns>
        public static IObservable<TwitterStatus> Find(Func<TwitterStatus, bool> predicate, FindRange<long> range = null, int? count = null)
        {
            if (_isInShutdown) return Observable.Empty<TwitterStatus>();
            return store.Find(predicate, range, count);
        }

        /// <summary>
        /// Remove tweet from store.
        /// </summary>
        /// <param name="id">removing tweet's id</param>
        /// <param name="publish">publish removing notification to children</param>
        public static void Remove(long id, bool publish = true)
        {
            if (_isInShutdown) return;
            if (publish)
                statusPublisher.OnNext(new StatusNotification() { IsAdded = false, StatusId = id });
            store.Remove(id);
        }

        /// <summary>
        /// Shutdown store.
        /// </summary>
        internal static void Shutdown()
        {
            _isInShutdown = true;
            store.Dispose();
            var pds = (PersistentDataStore<long, TwitterStatus>)store;
            StoreOnMemoryObjectPersistence.MakePersistent("statuses", pds.GetToCNIoPs());
        }
    }

    public class StatusNotification
    {
        /// <summary>
        /// flag of added status or removed
        /// </summary>
        public bool IsAdded { get; set; }

        /// <summary>
        /// status id.
        /// </summary>
        public long StatusId { get; set; }

        /// <summary>
        /// actual status.<para />
        /// this property is available when this notification notifys status is added.
        /// </summary>
        public TwitterStatus Status { get; set; }
    }
}
