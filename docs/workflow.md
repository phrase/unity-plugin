# Working with Phrase

Assuming that you are already using Unity Editor and its support for Localization, you probably already have localization set up in your Unity project. You have your languages set up, you have your String Table Collections and your game is happily switching between different languages and showing its content in those.

Now you want to improve your translation workflow by allowing the translation management (adding translations in different languages, reviewing and refining them etc) to happen outside of your Unity Editor. That's what Phrase can help with.

Here you can find out how to set up the connection between Unity and Phrase. How to use Phrase itself and the overview of all its features is outside of scope of this document and you can find out more [here](https://support.phrase.com/hc/en-us/categories/4930564750748-Phrase-Strings).

Here's the description of the processes/steps that you will be doing once and that you will be doing regularly:

## Once/occasionally

### Set up Phrase account and a project

* Sign up on Phrase Strings
* Create a [project](https://support.phrase.com/hc/en-us/articles/5784094677404-Projects-Strings)
* [Create an API key](https://support.phrase.com/hc/en-us/articles/5808341130268-Generate-API-Access-Token-Strings)

### Set up Phrase plugin

* Install the plugin as described in the [README](../README.md)
* Create Phrase provider asset as described in the [Usage](usage.md) document
* Connect to Phrase using your API key
* Choose your project
* Create the missing locales: if the list of locales in Unity doesn't match the list on Phrase, you'll be able to create missing ones on either side
* Choose which locales you want to push and which to pull: one possible approach is to maintain the source locale in Unity and push it to Phrase, and pull the target locales from Phrase to Unity when they are translated
* Connect the string tables

## Regularly

* Add new keys to your string tables, together with their translations in the source locale
* Add metadata such as description and maximum character length by using the [Phrase window](usage.md#phrase-window) or editing the metadata in the String Table Collection editor
* Push translations to Phrase. It will create all the missing keys in Phrase and update the translations
* Pull translations from Phrase to your application
* Optionally, you can upload [screenshots](https://support.phrase.com/hc/en-us/articles/5822309698204-Screenshot-Management-Strings) and attach them to the corresponding keys in Phrase, by using the Phrase window. The screenshots are then shown in the Phrase editor and can, along with the description, help the translators understand the context of the translation
