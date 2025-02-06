# Unity plugin for Phrase Strings integration

Unity plugin is a package for Unity which allows Unity developers to synchronize their localized strings in Unity with Phrase Strings.

## Installation

Phrase Unity plugin relies on Unity Localization package. It assumes that the Unity developer is using or plans to use localization through Unity Localization, in particular [String Table Collections](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/StringTables.html).

In order to get started with Unity Localization, refer to the [guide](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/QuickStartGuideWithVariants.html).

After you create initial set of locales and one or more String Table Collections, and connect them with some text objects (and/or start using them programmatically from your Unity scripts), you can install and start using Phrase Plugin, which can be installed in following ways:

* Install from the Asset Store (TBA)
* Install from package archive
  * Assets → Import Package → Custom Package… → choose `.unitypackage` file from disk
* Install from locally checked out source code
  * Either check out the source directly into `YourProject/Packages/com.phrase.plugin` directory, or
  * symlink `unity_plugin` directory into `YourProject/Packages` as `com.phrase.plugin`

## Usage

General workflow with Phrase Strings is described in [Workflow](docs/workflow.md) document, and detailed plugin usage instructions are in [Usage](docs/usage.md) document.

## Contributing

If you want to contribute, check out this project directly into `Packages`
directory of your project and rename the package directory to
`com.phrase.plugin`. You can also create a symlink to it instead.

### Publishing a new release

* Increase version number in `package.json` and `Editor/PhraseClient.cs`
* Update `CHANGELOG.md`
* Publish the release on the Asset Store
