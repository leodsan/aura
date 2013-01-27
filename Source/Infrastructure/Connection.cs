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
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using System.Configuration;
using System.IO;
using System.Threading;
using log4net;

namespace Aura {
	/// <summary>
	/// This class coordinates the connection strings for Aura.  By default it will read connection strings from the connectionSettings elements.
	/// You can retrieve connections by name. If there's only connection, it will default to that.
    /// If you are using DatabaseManager, name your DatabaseManager subclass the same as the connection string name
	/// </summary>
	public static class Connection {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		private static Dictionary<string, MongoUrl> connections;
		private static ReaderWriterLockSlim rwl = new ReaderWriterLockSlim();

		static Connection() {
			connections = new Dictionary<string, MongoUrl>();

            for(var i = 0; i < ConfigurationManager.ConnectionStrings.Count; i++)
            {
                ConnectionStringSettings connectionSetting = ConfigurationManager.ConnectionStrings[i];

                if (connectionSetting.ConnectionString.StartsWith("mongodb://"))
                {
                    string connectionName = connectionSetting.Name;
                    logger.Debug(String.Format("Initialized Aura Connection:{0} - {1}", connectionName, connectionSetting.ConnectionString));
                    connections.Add(connectionName, new MongoUrl(connectionSetting.ConnectionString));
                }
            }
		}

		/// <summary>
		/// Retrieves a MongoDatabase instance based upon the named connection.
		/// </summary>
		/// <param name="Name">The name of the connection (null if default connection)</param>
		public static MongoDatabase GetInstance(string Name = null) {
			MongoUrl mongoUrl = GetMongolUrlByName(Name ?? String.Empty);
            MongoClient client = new MongoClient(mongoUrl);
            return client.GetServer().GetDatabase(mongoUrl.DatabaseName);
		}

		/// <summary>
		/// Sets the value for a named connection.
		/// </summary>
		/// <param name="Name">The name of the new connection.</param>
		/// <param name="url">The MongoDB url for the connection.  Pass a value of null to remove the connection from the list.</param>
		public static void SetConnection(string Name, string url) {
			logger.Debug(String.Format("SetConnection({0},{1})", Name, url));
			rwl.EnterWriteLock();
			try {
				if (String.IsNullOrEmpty(url) && connections.ContainsKey(Name)) {
					connections.Remove(Name);
				}
				else {
					connections[Name ?? String.Empty] = new MongoUrl(url);
				}
			}
			finally {
				rwl.ExitWriteLock();
			}
		}

		private static MongoUrl GetMongolUrlByName(string Name) {

            if (String.IsNullOrEmpty(Name) && connections.Count == 1)
            {
                return connections.Values.Single();
            }

			rwl.EnterReadLock();
			try {
				if (!connections.ContainsKey(Name)) {
					throw new ConfigurationErrorsException("Missing ConnectionString " + Name);
				}
				return connections[Name];
			}
			finally {
				rwl.ExitReadLock();
			}
		}
	}
}
