using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Microsoft.Practices.ServiceLocation;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Bson;
using log4net;

namespace Aura
{
    /// <summary>
    /// Creates a unit of work implementation that can be used to wrap Mongol. Subclasses will automatically use the Connection Named after the subclass
    /// </summary>
    public abstract class DatabaseManager : IDatabaseManager
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Name of the Database
        /// </summary>
        public string DatabaseName { get; private set; }

        private Dictionary<Type, IRecordManager> RecordManagers { get; set; }

        /// <summary>
        /// Creates an instance of DatabaseManager
        /// </summary>
        public DatabaseManager()
        {
            InitDatabase();
            InitRecordManagers();
            InitSchema();
        }

        private void InitDatabase(){
            DatabaseName = GetType().Name;
            logger.Debug("Starting Database Manager for MongoDB database: " + DatabaseName);
        }

        internal IRecordManager<TRecord> GetRecordManager<TRecord>()
        {
            return (IRecordManager<TRecord>)RecordManagers[typeof(TRecord)];
        }

        private void InitRecordManagers()
        {
            bool hasServiceLocator = false;

            try {
                if(ServiceLocator.Current != null){
                    hasServiceLocator = true;
                }
            } catch(NullReferenceException){
                hasServiceLocator = false;
            }

            RecordManagers = new Dictionary<Type, IRecordManager>();

            var properties = GetType().GetProperties().Where(p => p.PropertyType.GetInterfaces().Contains(typeof(IRecordManager)));

            foreach (var property in properties)
            {
                logger.Debug("Initializing Record Manager: " + property.Name + " (of type: " + property.PropertyType + ")");

                Type type;

                object instance = null;
                Type genericType = null;

                if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(IRecordManager<>))
                {
                    genericType = property.PropertyType.GetGenericArguments()[0];
                }
                else if (property.PropertyType.GetInterfaces().Any(p => p.IsGenericType && p.GetGenericTypeDefinition() == typeof(IRecordManager<>)))
                {
                    genericType = property.PropertyType.GetInterfaces().First(p => p.IsGenericType).GetGenericArguments()[0];
                }

                if (hasServiceLocator)
                {
                    try
                    {
                        instance = ServiceLocator.Current.GetInstance(property.PropertyType);
                    }
                    catch (ActivationException)
                    {
                        instance = null;
                    }
                }

                if (instance == null && property.PropertyType.IsInterface)
                {
                    type = typeof(RecordManager<>).MakeGenericType(genericType);
                    instance = Activator.CreateInstance(type,(object)true);
                }

                if (instance == null && property.PropertyType.IsClass)
                {
                    var dbRecordConstructor = property.PropertyType.GetConstructor(new Type[] { typeof(Boolean) });

                    if (dbRecordConstructor != null)
                    {
                        instance = dbRecordConstructor.Invoke(new object[] { true });
                    }
                    else
                    {
                        instance = Activator.CreateInstance(property.PropertyType);
                    }
                }

                if (instance is RecordManager)
                {
                    RecordManager recordManagerInstance = (RecordManager)instance;
                    recordManagerInstance.SetupManager(collectionName: null, connectionName: DatabaseName);
                }

                if (instance == null)
                {
                    throw new ApplicationException("Could not initialize an instance for: " + property.Name);
                }
                else
                {
                    property.SetValue(this, instance, null);

                    if (genericType != null)
                    {
                        RecordManagers[genericType] = (IRecordManager)instance;
                    }
                }
            }
        }

        private void InitSchema()
        {
            var recordManagers = RecordManagers.Select(rm => rm.Value).OfType<RecordManager>();

            foreach (var recordManager in recordManagers)
            {
                var collection = recordManager.GetCollection();

                if (recordManager.Indexes != null)
                {
                    var indexes = collection.GetIndexes().ToList();
                    var neededIndexes = recordManager.Indexes;

                    foreach (var index in indexes)
                    {
                        if (index.Name == "_id_") continue;

                        var key = index.Key;

                        bool doRemove = true;

                        foreach (var neededIndex in neededIndexes)
                        {

                            BsonDocument neededIndexKeysDoc = neededIndex.ToBsonDocument().GetElement("Keys").Value.ToBsonDocument();

                            BsonValue neededIndexOptionsValue = neededIndex.ToBsonDocument().GetElement("Options").Value;
                            BsonDocument neededIndexOptionsDoc = null;

                            if (!neededIndexOptionsValue.IsBsonNull)
                            {
                                neededIndexOptionsDoc = neededIndex.ToBsonDocument().GetElement("Options").Value.ToBsonDocument();
                            }

                            int matchedKeys = 0;
                            int totalKeysInIndex = key.Elements.Count();

                            foreach (var elem in key.Elements)
                            {
                                if (elem.Name == "_ftsx")
                                {
                                    continue;
                                }

                                if (elem.Name == "_fts")
                                {
                                     var weights = index.RawDocument.GetElement("weights");

                                    var fullTextFields = neededIndexKeysDoc.Where(d => d.Value == "text").Select(x => x.Name);
                                    totalKeysInIndex = weights.Value.AsBsonDocument.Elements.Count();

                                    foreach (var field in fullTextFields)
                                    {
                                        BsonElement matchedElem = null;

                                        if (weights.Value.AsBsonDocument.TryGetElement(field, out matchedElem))
                                        {
                                            matchedKeys++;
                                        }
                                    }

                                } else {

                                    var field = elem.Name;
                                    int direction = elem.Value.AsInt32;

                                    BsonElement matchedElem = null;

                                    if (neededIndexKeysDoc.TryGetElement(field,out matchedElem))
                                    {
                                        if (matchedElem.Value.AsInt32 == direction)
                                        {
                                            matchedKeys++;
                                        }
                                    }
                                }
                            }

                            if (matchedKeys == totalKeysInIndex && matchedKeys == neededIndexKeysDoc.Elements.Count())
                            {
                                doRemove = false;

                                bool isUnique = false;
                                bool isSparse = false;

                                BsonElement value;

                                if (neededIndexOptionsDoc != null)
                                {
                                    if (neededIndexOptionsDoc.TryGetElement("unique", out value))
                                    {
                                        isUnique = value.Value.ToBoolean();
                                    }

                                    if (neededIndexOptionsDoc.TryGetElement("sparse", out value))
                                    {
                                        isSparse = value.Value.ToBoolean();
                                    }
                                }

                                if (index.IsUnique != isUnique)
                                {
                                    doRemove = true;
                                }

                                if (index.IsSparse != isSparse)
                                {
                                    doRemove = true;
                                }

                                break;
                            }
                        }

                        if (doRemove)
                        {
                            logger.Debug("Dropping unused index " + index.Name + " from collection " + collection.Name);
                            collection.DropIndexByName(index.Name);
                        }
                    }

                    foreach (var index in recordManager.Indexes)
                    {
                        collection.EnsureIndex(index.Keys, index.Options);
                    }
                }

                if (recordManager.RemovedFields != null)
                {
                    foreach (var removedField in recordManager.RemovedFields)
                    {
                        var removedFieldName = removedField.Key;
                        var removedFieldCallback = removedField.Value;

                        if (collection.Count(Query.Exists(removedFieldName)) > 0)
                        {
                            logger.Debug("Removing field " + removedField + " from collection " +  collection.Name);

                            if (removedFieldCallback != null)
                            {
                                removedFieldCallback(collection.FindAs<BsonDocument>(Query.Exists(removedFieldName)));
                            }

                            collection.Update(Query.Null, Update.Unset(removedFieldName), UpdateFlags.Multi);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets a reference to the internal MongoDatabase from the DatabaseManager
        /// </summary>
        /// <returns></returns>
        public MongoDatabase GetDatabase()
        {
            return Connection.GetInstance(DatabaseName);
        }
    }
}
