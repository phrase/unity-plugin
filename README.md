# Unity plugin for Phrase Strings integration

The plugin builds on top of Unity "Localization" package and offers importing
and exporting translations from your String Table Collections to your Phrase
Strings projects.

## Development

Install the plugin into your unity project from your working directory by
going to "Package manager", then "Add package from disk", and then choose
`package.json` of this project. It will also install Localization package
as a dependency, if not already installed.

If you want to contribute, check out this project directly into `Packages`
directory of your project and rename the package directory to
`com.phrase.plugin`. You can also create a symlink to it instead.

### Publishing a new release

* Increase version number in `package.json` and `Editor/PhraseClient.cs`
* Update `CHANGELOG.md`
* Publish the release on the Asset Store
