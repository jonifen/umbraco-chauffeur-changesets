# umbraco-chauffeur-changesets

 * Clone repo
 * Open in Visual Studio
 * Make sure NuGets are restored.
 * Build
 * Chauffeur.Runner.exe delivery -p:adminpwd=umbracocms

Now run the project (Ctrl+F5 to run without debugging), login as admin/umbracocms

The '20180525_0237-Changes' delivery *should* cause the 'My Second Textbox' to be removed as it's been removed from the Document Type, but it still remains.

