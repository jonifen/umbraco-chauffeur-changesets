using System;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using umbraco_chauffeur_changesets.Chauffeur;
using umbraco_chauffeur_changesets.Constants;

namespace umbraco_chauffeur_changesets.UmbracoOverrides
{
    public class EntityEventHandler : ApplicationEventHandler
    {
        private DatabaseSchemaHelper _dbSchemaHelper;
        private UmbracoDatabase _database;

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            ConfigureDatabase();

            Umbraco.Core.Services.ContentTypeService.SavedContentType += ContentTypeService_SavedContentType;
            Umbraco.Core.Services.DataTypeService.Saved += DataTypeService_Saved;
            Umbraco.Core.Services.FileService.SavedTemplate += FileService_SavedTemplate;
            Umbraco.Core.Services.FileService.SavedPartialView += FileService_SavedPartialView;
            Umbraco.Core.Services.MacroService.Saved += MacroService_Saved;

            Umbraco.Core.Services.ContentTypeService.DeletedContentType += ContentTypeService_DeletedContentType;
            Umbraco.Core.Services.DataTypeService.Deleted += DataTypeService_Deleted;
            Umbraco.Core.Services.FileService.DeletedTemplate += FileService_DeletedTemplate;
            Umbraco.Core.Services.FileService.DeletedPartialView += FileService_DeletedPartialView;
            Umbraco.Core.Services.MacroService.Deleted += MacroService_Deleted;

            base.ApplicationStarted(umbracoApplication, applicationContext);
        }

        #region Saved Events
        private void FileService_SavedPartialView(Umbraco.Core.Services.IFileService sender, Umbraco.Core.Events.SaveEventArgs<Umbraco.Core.Models.IPartialView> e)
        {
            var entity = e.SavedEntities.FirstOrDefault();
            SaveEntityChange(EntityType.PARTIAL_VIEW, entity.Name);
        }

        private void FileService_SavedTemplate(Umbraco.Core.Services.IFileService sender, Umbraco.Core.Events.SaveEventArgs<Umbraco.Core.Models.ITemplate> e)
        {
            var entity = e.SavedEntities.FirstOrDefault();
            SaveEntityChange(EntityType.TEMPLATE, entity.Name);
        }

        private void DataTypeService_Saved(Umbraco.Core.Services.IDataTypeService sender, Umbraco.Core.Events.SaveEventArgs<Umbraco.Core.Models.IDataTypeDefinition> e)
        {
            var entity = e.SavedEntities.FirstOrDefault();
            SaveEntityChange(EntityType.DATA_TYPE, entity.Name);
        }

        private void ContentTypeService_SavedContentType(Umbraco.Core.Services.IContentTypeService sender, Umbraco.Core.Events.SaveEventArgs<Umbraco.Core.Models.IContentType> e)
        {
            var entity = e.SavedEntities.FirstOrDefault();
            SaveEntityChange(EntityType.CONTENT_TYPE, entity.Name);
        }

        private void MacroService_Saved(Umbraco.Core.Services.IMacroService sender, Umbraco.Core.Events.SaveEventArgs<Umbraco.Core.Models.IMacro> e)
        {
            var entity = e.SavedEntities.FirstOrDefault();
            SaveEntityChange(EntityType.MACRO, entity.Name);
        }

        private void SaveEntityChange(EntityType type, string entityName)
        {
            LogHelper.Info(typeof(EntityEventHandler), $"{type.ToString()} '{entityName}' has been saved.");
            AddEntityToChangedTable(type, entityName);
        }

        #endregion Saved Events

        #region Deleted Events

        private void FileService_DeletedPartialView(Umbraco.Core.Services.IFileService sender, Umbraco.Core.Events.DeleteEventArgs<Umbraco.Core.Models.IPartialView> e)
        {
            var entity = e.DeletedEntities.FirstOrDefault();
            DeleteEntityChange(EntityType.PARTIAL_VIEW, entity.Name);
        }

        private void FileService_DeletedTemplate(Umbraco.Core.Services.IFileService sender, Umbraco.Core.Events.DeleteEventArgs<Umbraco.Core.Models.ITemplate> e)
        {
            var entity = e.DeletedEntities.FirstOrDefault();
            DeleteEntityChange(EntityType.TEMPLATE, entity.Name);
        }

        private void DataTypeService_Deleted(Umbraco.Core.Services.IDataTypeService sender, Umbraco.Core.Events.DeleteEventArgs<Umbraco.Core.Models.IDataTypeDefinition> e)
        {
            var entity = e.DeletedEntities.FirstOrDefault();
            DeleteEntityChange(EntityType.DATA_TYPE, entity.Name);
        }

        private void ContentTypeService_DeletedContentType(Umbraco.Core.Services.IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<Umbraco.Core.Models.IContentType> e)
        {
            var entity = e.DeletedEntities.FirstOrDefault();
            DeleteEntityChange(EntityType.CONTENT_TYPE, entity.Name);
        }

        private void MacroService_Deleted(Umbraco.Core.Services.IMacroService sender, Umbraco.Core.Events.DeleteEventArgs<Umbraco.Core.Models.IMacro> e)
        {
            var entity = e.DeletedEntities.FirstOrDefault();
            DeleteEntityChange(EntityType.MACRO, entity.Name);
        }

        private void DeleteEntityChange(EntityType type, string entityName)
        {
            LogHelper.Info(typeof(EntityEventHandler), $"{type.ToString()} '{entityName}' has been deleted.");
            RemoveEntityFromChangedTable(type, entityName);
        }

        #endregion Deleted Events

        private void ConfigureDatabase()
        {
            var dbContext = ApplicationContext.Current.DatabaseContext;
            var logger = LoggerResolver.Current.Logger;
            _database = dbContext.Database;
            _dbSchemaHelper = new DatabaseSchemaHelper(dbContext.Database, logger, dbContext.SqlSyntax);

            if (!_dbSchemaHelper.TableExist("Chauffeur_Changes"))
            {
                _dbSchemaHelper.CreateTable<ChauffeurChangesTable>(false);
            }
        }

        private void AddEntityToChangedTable(EntityType type, string name)
        {
            var changedEntities = _database.Fetch<ChauffeurChangesTable>("SELECT * FROM Chauffeur_Changes");
            var filteredChangedEntities = changedEntities.Where(c => c.EntityType == Convert.ToInt32(type) && c.Name == name).ToList();

            if (filteredChangedEntities.Count == 0)
            {
                _database.Save(new ChauffeurChangesTable
                {
                    EntityType = Convert.ToInt32(type),
                    Name = name,
                    ChangeDate = DateTime.Now,
                    ChangeType = Convert.ToInt32(ChangeType.SAVED)
                });
            }
        }

        private void RemoveEntityFromChangedTable(EntityType type, string name)
        {
            // Nothing to see here... yet.
        }
    }
}