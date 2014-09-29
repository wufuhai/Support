using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FixReferences {
    class NugetUpdater:Updater {
        readonly string _version;
        private readonly string[] _projects;
        private readonly string[] _nuspecs;
        internal readonly XNamespace XNamespace = XNamespace.Get("http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd");

        public NugetUpdater(IDocumentHelper documentHelper, string rootDir, string version, string[] projects, string[] nuspecs)
            : base(documentHelper, rootDir){
            _version = version;
            _projects = projects;
            _nuspecs = nuspecs;
        }

        private IEnumerable<string> GetProjects(string file) {
            var nuspecFileNames = GetNuspecFiles(file);
            var projects = _projects.Where(s => nuspecFileNames.Contains(AdjustName((Path.GetFileNameWithoutExtension(s))))).ToArray();
            if (!projects.Any())
                throw new NotImplementedException(file);
            return projects;
        }

        private HashSet<string> GetNuspecFiles(string file){
            var hashSet = new HashSet<string>();
            var nuspecFileName = (Path.GetFileNameWithoutExtension(file) + "").ToLowerInvariant();
            if (nuspecFileName.StartsWith("system"))
                hashSet.Add(nuspecFileName.Replace("system.", "").Replace("system", ""));
            else if (nuspecFileName == "lib"){
                hashSet.Add("utils");
                hashSet.Add("xpo");
                hashSet.Add("persistent.base");
                hashSet.Add("persistent.baseimpl");
            }
            else{
                hashSet.Add(nuspecFileName);
            }
            return hashSet;
        }

        private string AdjustName(string name){
            return name.Replace("Xpand.ExpressApp.", "").Replace("Xpand.ExpressApp", "").Replace("Xpand.", "").ToLowerInvariant();
        }

        public override void Update(string file) {
            var document = DocumentHelper.GetXDocument(file);

            var projects = GetProjects(file).ToArray();
            var allReferences = GetReferences(projects).ToArray();
            var dependencies = GetDependencies(allReferences).ToArray();
            UpdateDependencies(document,dependencies);
            UpdateFiles(document, allReferences,dependencies.Select(pair => pair.Key));

//            var versionElement = document.Descendants().First(element => element.Name.LocalName == "version");
//            versionElement.Value = _version;
//            var dependenciesElement = document.Descendants().FirstOrDefault(element => element.Name.LocalName.ToLower() == "dependencies");
//            if (dependenciesElement != null)
//                foreach (var element in dependenciesElement.Elements()) {
//                    element.SetAttributeValue("version",_version);
//                }
            DocumentHelper.Save(document, file);
        }

        private void UpdateFiles(XDocument document, KeyValuePair<string, IEnumerable<XElement>>[] allReferences, IEnumerable<XElement> dependencies){
            var filesElement = document.Descendants(XNamespace + "files").First();
            for (int index = filesElement.DescendantNodes().ToArray().Length - 1; index >= 0; index--) {
                var descendantNode = filesElement.DescendantNodes().ToArray()[index];
                descendantNode.Remove();
            }
            CreateMainFiles(allReferences, filesElement);
            CreateReferenceFiles(allReferences, dependencies, filesElement);
        }

        private void CreateReferenceFiles(IEnumerable<KeyValuePair<string, IEnumerable<XElement>>> allReferences, IEnumerable<XElement> dependencies, XElement filesElement){
            var elements = allReferences.SelectMany(pair => pair.Value).Except(dependencies);
            foreach (var assemblyName in elements.Select(GetAssemblyName)){
                filesElement.Add(NewFileElement(assemblyName));
            }
        }

        private void CreateMainFiles(IEnumerable<KeyValuePair<string, IEnumerable<XElement>>> allReferences, XElement filesElement){
            foreach (var xDocument in allReferences.OrderBy(pair => Path.GetFileNameWithoutExtension(pair.Key))
                        .Select(pair => XDocument.Load(pair.Key))){
                var assemblyName = xDocument.Descendants(ProjectUpdater.XNamespace + "AssemblyName").First().Value;
                filesElement.Add(NewFileElement(assemblyName));
                filesElement.Add(NewFileElement(assemblyName, ".pdb"));
            }
        }

        private XElement NewFileElement(string assemblyName,string extension=".dll"){
            var content = new XElement(XNamespace + "file");
            content.SetAttributeValue("src", @"\Build\Temp\" + assemblyName + extension);
            content.SetAttributeValue("target", @"lib\net40\" + assemblyName + extension);
            return content;
        }

        private void UpdateDependencies(XDocument document, IEnumerable<KeyValuePair<XElement, string>> elements){
            var metadataElement = document.Descendants(XNamespace+"metadata").First();
            metadataElement.Descendants(XNamespace+"dependencies").Remove();
            var dependenciesElement = new XElement(XNamespace + "dependencies");
            var packages = elements.Select(pair => new { Name = GetAssemblyName(pair.Key), Version = pair.Value }).Select(arg => new { Id = PackageId(arg.Name),arg.Version }).OrderBy(arg => arg.Id).GroupBy(arg => arg.Id).Select(grouping => grouping.First());
            foreach (var package in packages) {
                var dependencyElement = new XElement(XNamespace + "dependency");
                dependencyElement.SetAttributeValue("id",package.Id);
                dependencyElement.SetAttributeValue("version",package.Version);
                dependenciesElement.Add(dependencyElement);
            }
            metadataElement.Add(dependenciesElement);
        }

        private string PackageId(string assemblyName){
            if (assemblyName.StartsWith("Xpand")){
                var adjustName = AdjustName(assemblyName).ToLowerInvariant();
                var nuspec = FindNuspec(adjustName);
                if (nuspec == null){
                    var strings = new []{"utils","xpo","persistent.base","persistent.baseimpl","emailtemplateengine"};
                    if (strings.Any(adjustName.Contains))
                        nuspec = FindNuspec("lib");
                    else if (adjustName.Contains("win"))
                        nuspec = FindNuspec("system.win");
                    else if (adjustName.Contains("web"))
                        nuspec = FindNuspec("system.web");
                    else if (adjustName == "")
                        nuspec = FindNuspec("system");
                }
                return XDocument.Load(nuspec).Descendants(XNamespace + "id").First().Value;
            }
            return assemblyName;
        }

        private string FindNuspec(string adjustName){
            return _nuspecs.FirstOrDefault(s => adjustName == (Path.GetFileNameWithoutExtension(s)+"").ToLowerInvariant());
        }


        private IEnumerable<KeyValuePair<XElement,string>> GetDependencies(IEnumerable<KeyValuePair<string, IEnumerable<XElement>>> references) {
            var elements = new List<KeyValuePair<XElement, string>>();
            foreach (var reference in references){
                var path = Path.Combine(Path.GetDirectoryName(reference.Key)+"", "packages.config");
                foreach (var element in reference.Value){
                    var assemblyName = GetAssemblyName(element).ToLowerInvariant();
                    var version = _version;
                    if(!assemblyName.StartsWith("xpand")) {
                        var packagesConfig = (File.Exists(path) ? File.ReadAllText(path) : "").ToLowerInvariant();
                        var regex = new Regex("<package id=\"" +assemblyName+ "\" .*version=\"([^\"]*)", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
                        var match = regex.Match(packagesConfig);
                        if (match.Success){
                            version = match.Groups[1].Value;
                        }
                        else{
                            continue;
                        }
                    }
                    elements.Add(new KeyValuePair<XElement, string>(element, version));
                }
            }
            return elements;
        }

        private string GetAssemblyName(XElement element){
            var value = element.Attribute("Include").Value;
            var indexOf = value.IndexOf(",", StringComparison.Ordinal);
            return indexOf == -1 ? value : value.Substring(0, indexOf);
        }

        private IEnumerable<KeyValuePair<string,IEnumerable<XElement>>> GetReferences(IEnumerable<string> projects){
            return projects.Select(s =>{
                var document = XDocument.Load(s);
                var strings = new []{"DevExpress","System","Microsoft"};
                var xElements = document.Descendants(ProjectUpdater.XNamespace + "Reference")
                    .Where(element => !strings.Any(s1 => element.Attribute("Include").Value.StartsWith(s1)));
                return new KeyValuePair<string, IEnumerable<XElement>>(s, xElements);
            });
        }

    }

}
