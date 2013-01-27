/* Copyright 2012 Ephisys Inc.
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
   limitations under the License.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Bson;

namespace Aura {
	/// <summary>
	/// Works like a standard RecordManager, except that it keeps a cache of all records.  Well suited for lookups collections
	/// that have a relatively small number of objects that are frequently retrieved for read-only activity.
	/// </summary>
	/// <remarks>Calling Save() will in addition to saving the object, also update the item in the cache. Calling Delete() will also remove the item from the cache.</remarks>
	public class CachingRecordManager<T> : RecordManager<T> where T : class {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private Dictionary<object, T> cache = new Dictionary<object, T>();

        private bool hasPopulatedCache = false;

        /// <summary>
        /// Creates a new RecordManager
        /// </summary>
        public CachingRecordManager(bool isDbManaged)
            : base(isDbManaged)
        {
        }

        /// <summary>
        /// Creates a new CachingRecordManager
        /// </summary>
        public CachingRecordManager(string collectionName = null, string connectionName = null)
            : base(collectionName, connectionName)
        {
        }

		/// <summary>
		/// Retrieves an object by Id and caches the retrieval by Id for future lookup.
		/// </summary>
		public override T GetById(object Id) {

            if (!hasPopulatedCache)
            {
                PopulateCache();
            }

			return cache[Id];
		}

        /// <summary>
        /// Gets all items in database
        /// </summary>
        /// <returns></returns>
        public IEnumerable<T> GetAll()
        {
            if (!hasPopulatedCache)
            {
                PopulateCache();
            }

            return cache.Values.ToList();
        }

        protected IEnumerable<T> Values
        {
            get
            {
                if (!hasPopulatedCache)
                {
                    PopulateCache();
                }

                return cache.Values;
            }
        }

        private void PopulateCache()
        {
            lock (cache)
            {
                if (!hasPopulatedCache)
                {
                    var items = Find(Query.Null);

                    foreach (var item in items)
                    {
                        var id = GetRecordId(item);
                        cache.Add(id, item);
                    }

                    hasPopulatedCache = true;
                }
            }
        }

		/// <summary>
		/// Removes all items from the cache.
		/// </summary>
		public void ClearCache() {
			logger.Debug("ClearCache()");
			lock (cache) {
				cache.Clear();
                hasPopulatedCache = false;
			}
		}

		/// <summary>
		/// Deletes the item in the data store and also removes the item from the cache.
		/// </summary>
		public override void DeleteById(object id) {
			logger.Debug(String.Format("DeleteById({0})", id));
			base.DeleteById(id);
			if (cache.ContainsKey(id)) {
                lock (cache)
                {
                    cache.Remove(id);
                }
			}
		}

		/// <summary>
		/// Updates the item in the data store and also updates the item in the cache.
		/// </summary>
		public override bool Save(T record) {
			logger.Debug(String.Format("Save({0})", record));
			var wasInsert = base.Save(record);
			lock (cache) {
				cache[GetRecordId(record)] = record;
			}
			return wasInsert;
		}
	}
}
