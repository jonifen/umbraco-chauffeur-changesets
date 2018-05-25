using Chauffeur;
using Chauffeur.Host;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;
using Umbraco.Core.Services;
using umbraco_chauffeur_changesets.Constants;

namespace umbraco_chauffeur_changesets.Chauffeur
{
    [DeliverableName("changes")]
    public class ChangesDeliverable : Deliverable, IProvideDirections
    {
        public const string TableName = "Chauffeur_Changes";

        private DatabaseSchemaHelper _dbSchemaHelper;
        private Database _database;
        private IChauffeurSettings _settings;
        private IFileSystem _fileSystem;
        private IContentTypeService _contentTypeService;
        private IDataTypeService _dataTypeService;
        private IPackagingService _packagingService;
        private IFileService _fileService;
        private IMacroService _macroService;
        private List<ChauffeurChangesTable> _changedEntities;

        public ChangesDeliverable(
            TextReader reader,
            TextWriter writer,
            IChauffeurSettings settings,
            IFileSystem fileSystem,
            IContentTypeService contentTypeService,
            IDataTypeService dataTypeService,
            IFileService fileService,
            IMacroService macroService,
            IPackagingService packagingService,
            DatabaseSchemaHelper dbSchemaHelper,
            Database database
        ) : base(reader, writer)
        {
            _settings = settings;
            _fileSystem = fileSystem;
            _contentTypeService = contentTypeService;
            _dataTypeService = dataTypeService;
            _packagingService = packagingService;
            _fileService = fileService;
            _macroService = macroService;
            _dbSchemaHelper = dbSchemaHelper;
            _database = database;

            GetChangedEntityNames();
        }

        public async override Task<DeliverableResponse> Run(string command, string[] args)
        {
            if (!EntitiesHaveBeenChanged())
            {
                await Out.WriteLineAsync("No Changes have been made. Nothing has been written to disk.");
                return DeliverableResponse.Continue;
            }

            var useDefaults = args.Any() && args[0] == "default";

            var name = useDefaults ?
                GetChangesDefaultSettings() :
                await GetChangesSettings();

            using (var deliveryFileStream = CreateChangesFile(name))
            {
                await CreatePackage(deliveryFileStream, name);
            }

            await Out.WriteLineAsync("All changes have been packaged. You can now deliver the changes on the destination Umbraco instance:");
            await Out.WriteLineAsync("./Chauffeur.Runner.exe delivery");

            return DeliverableResponse.Continue;
        }

        private bool EntitiesHaveBeenChanged()
        {
            if (_changedEntities == null || _changedEntities.Count == 0)
                return false;
            return true;
        }

        private async System.Threading.Tasks.Task CreatePackage(StreamWriter deliveryFileStream, string name)
        {
            var contentTypes = GetEntityChanges(_contentTypeService.GetAllContentTypes(), EntityType.CONTENT_TYPE);
            var dataTypes = GetEntityChanges(_dataTypeService.GetAllDataTypeDefinitions(), EntityType.DATA_TYPE);
            var templates = GetFileChanges(_fileService.GetTemplates(), EntityType.TEMPLATE);
            var styleSheets = GetFileChanges(_fileService.GetStylesheets(), EntityType.STYLESHEET);
            var macros = GetChangedMacros();

            var packageXml = new XDocument();
            packageXml.Add(
                new XElement(
                    "umbPackage",
                    new XElement(
                        "DocumentTypes",
                        contentTypes.Select(ct => _packagingService.Export(ct, false))
                    ),
                    _packagingService.Export(dataTypes, false),
                    _packagingService.Export(templates, false),
                    _packagingService.Export(macros, false),
                    new XElement(
                        "Stylesheets",
                        styleSheets.Select(s =>
                            new XElement(
                                "Stylesheet",
                                new XElement("Name", s.Alias),
                                new XElement("FileName", s.Name),
                                new XElement("Content", new XCData(s.Content)),
                                new XElement(
                                    "Properties",
                                    s.Properties.Select(p =>
                                        new XElement(
                                            "Property",
                                            new XElement("Name", p.Name),
                                            new XElement("Alias", p.Alias),
                                            new XElement("Value", p.Value)
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            );

            _settings.TryGetChauffeurDirectory(out string dir);
            _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(dir, $"{name}.xml"), packageXml.ToString());
            await deliveryFileStream.WriteLineAsync($"package {name}");

            _database.TruncateTable("Chauffeur_Changes");
        }

        private void GetChangedEntityNames()
        {
            _changedEntities = _database.Fetch<ChauffeurChangesTable>("SELECT * FROM Chauffeur_Changes");
        }

        private IEnumerable<T> GetEntityChanges<T>(IEnumerable<T> allOfType, EntityType type) where T : IUmbracoEntity
        {
            var changed = _changedEntities.Where(ce => ce.EntityType == Convert.ToInt32(type));
            if (changed.Count() > 0)
                return allOfType.Where(t => changed.Select(c => c.Name).Contains(t.Name));
            return new List<T>();
        }

        private IEnumerable<T> GetFileChanges<T>(IEnumerable<T> allOfType, EntityType type) where T : IFile
        {
            var changed = _changedEntities.Where(ce => ce.EntityType == Convert.ToInt32(type));
            if (changed.Count() > 0)
                return allOfType.Where(t => changed.Select(c => c.Name).Contains(t.Name));
            return new List<T>();
        }

        private IEnumerable<IMacro> GetChangedMacros()
        {
            var allOfType = _macroService.GetAll();
            var changed = _changedEntities.Where(ce => ce.EntityType == Convert.ToInt32(EntityType.MACRO));
            if (changed.Count() > 0)
                return allOfType.Where(t => changed.Select(c => c.Name).Contains(t.Name));
            return new List<IMacro>();
        }

        private StreamWriter CreateChangesFile(string name)
        {
            _settings.TryGetChauffeurDirectory(out string dir);
            var file = _fileSystem.FileInfo.FromFileName(
                _fileSystem.Path.Combine(dir, $"{name}.delivery")
            );
            return file.CreateText();
        }

        private async Task<string> GetChangesSettings()
        {
            var defaultFileName = $"{DateTime.Now.ToString("yyyyMMdd_hhmm")}-Changes";
            await Out.WriteLineAsync("Time to output Chauffeur changes!");
            await Out.WriteAsync($"What do you want the name to be ({defaultFileName})? ");
            var name = await In.ReadLineWithDefaultAsync(defaultFileName);

            return (name);
        }

        private string GetChangesDefaultSettings() =>
            ($"{DateTime.Now.ToString("yyyyMMdd_hhmm")}-Changes");

        public async System.Threading.Tasks.Task Directions()
        {
            await Out.WriteLineAsync("changes");
            await Out.WriteLineAsync("\tAllows you to output changes that have been tracked since last scaffold/changeset.");
        }
    }

    [TableName(ChangesDeliverable.TableName)]
    [PrimaryKey("Id")]
    public class ChauffeurChangesTable
    {
        [Column("Id")]
        [PrimaryKeyColumn(Name = "PK_id", IdentitySeed = 1)]
        public int Id { get; set; }

        [Column("EntityType")]
        public int EntityType { get; set; }

        [Column("Name")]
        public string Name { get; set; }

        [Column("ChangeDate")]
        public DateTime ChangeDate { get; set; }

        [Column("ChangeType")]
        public int ChangeType { get; set; }
    }
}