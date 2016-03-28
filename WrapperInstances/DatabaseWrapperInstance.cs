﻿using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

using Aragas.Core.Wrappers;

using FileDbNs;

using SQLite.Net;
using SQLite.Net.Interop;
using SQLite.Net.Platform.Generic;

namespace PokeD.Server.Desktop.WrapperInstances
{
    /// <summary>
    /// SQL Database, only Primitive Types.
    /// </summary>
    public sealed class SQLiteDatabase : Database
    {
        public override string FileExtension => ".sqlite3";

        private SQLiteConnection Connection { get; set; }


        public override Database Create(string databaseName)
        {
            Connection = new SQLiteConnection(new SQLitePlatformGeneric(), CombinePath(databaseName));

            return this;
        }
        
        public override void CreateTable<T>()
        {
            var flags = CreateFlags.None;

            var prop = typeof (T).GetProperty("Id");
            if (Attribute.IsDefined(prop, typeof (PrimaryKeyAttribute)))
                flags |= CreateFlags.ImplicitPK;
            if (Attribute.IsDefined(prop, typeof (AutoIncrementAttribute)))
                flags |= CreateFlags.AutoIncPK;

            Connection.CreateTable<T>(flags);
        }

        public override void Insert<T>(T obj) { Connection.Insert(obj); }

        public override void Update<T>(T obj) { Connection.Update(obj); }

        public override T Find<T>(Expression<Func<T, bool>> predicate) => Connection.Find(predicate);


        private string CombinePath(string fileName) { return Path.Combine(FileSystemWrapper.DatabaseFolder.Path, fileName + FileExtension); }
    }

    /// <summary>
    /// NoSQL Database, only Primitive Types.
    /// </summary>
    public sealed class FileDBDatabase : Database
    {
        public override string FileExtension => ".fdb";

        private FileDb Database { get; set; }


        public override Database Create(string databaseName)
        {
            Database = new FileDb();

            return this;
        }
        
        public override void CreateTable<T>()
        {
            Database.Create(CombinePath(typeof(T).Name), CreateFields(new T()));
            Database.Close();
        }

        public override void Insert<T>(T obj)
        {
            Database.Open(CombinePath(typeof(T).Name), false);
            Database.AddRecord(CreateFieldValues(obj));
            Database.Close();
        }

        public override void Update<T>(T obj)
        {
            Database.Open(CombinePath(typeof(T).Name), false);

            var idProp = obj.GetType().GetProperties().FirstOrDefault(property => property.Name == "Id");
            if (idProp != null)
                Database.UpdateRecords(new FilterExpression("Id", idProp.GetValue(obj), ComparisonOperatorEnum.Equal), CreateFieldValues(obj));

            Database.Close();
        }

        public override T Find<T>(Expression<Func<T, bool>> predicate)
        {
            Database.Open(CombinePath(typeof(T).Name), true);

            var table = Database.SelectAllRecords();
            if (table.Count > 0)
            {
                var function = predicate.Compile();
                var result = table.Select(CreateT<T>).FirstOrDefault(function);
                Database.Close();
                return result;
            }
            else
            {
                Database.Close();
                return null;
            }
        }


        private string CombinePath(string fileName) { return Path.Combine(FileSystemWrapper.DatabaseFolder.Path, fileName + FileExtension); }


        private static Fields CreateFields(object obj)
        {
            var fields = new Fields();

			foreach (var info in obj.GetType().GetProperties())
            {
                DataTypeEnum dataType;

                if (info.PropertyType.IsEnum || info.PropertyType == typeof(Int16) || info.PropertyType == typeof(UInt16))
                    dataType = DataTypeEnum.Int32;
				else if (info.PropertyType == typeof(bool))
                    dataType = DataTypeEnum.Bool;
                else if (info.PropertyType == typeof(Guid))
                    dataType = DataTypeEnum.String;
                else
					if (!Enum.TryParse(info.PropertyType.Name, out dataType))
                        throw new Exception("Only Primitive Types supported!");


                var field = new Field(info.Name, dataType);
                if (info.Name == "Id")
                {
                    if (Attribute.IsDefined(info, typeof(PrimaryKeyAttribute)))
                        field.IsPrimaryKey = true;

                    if (Attribute.IsDefined(info, typeof (AutoIncrementAttribute)))
                        field.AutoIncStart = 1;
                }

                fields.Add(field);
            }
            return fields;
        }

        private static FieldValues CreateFieldValues(object obj)
        {
            var fieldValues = new FieldValues();

			foreach (var propertyInfo in obj.GetType().GetProperties())
                fieldValues.Add(propertyInfo.Name, propertyInfo.GetValue(obj));

            return fieldValues;
        }

        private static T CreateT<T>(Record record) where T : class, new()
        {
            var instance = new T();

			foreach (var info in instance.GetType().GetProperties())
                info.SetValue(instance, record[info.Name]);

            return instance;
        }
    }
}
