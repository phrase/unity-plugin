# Working with Phrase

Assuming that you are already using Unity Editor and its support for Localization, you probably already have localization set up in your Unity project. You have your languages set up, you have your String Table Collections and your game is happily switching between different languages and showing its content in those.

Now you want to improve your translation workflow by allowing the translation management (adding translations in different languages, reviewing and refining them etc) to happen outside of your Unity Editor. That's what Phrase can help with.

Here you can find out how to set up the connection between Unity and Phrase. How to use Phrase itself and the overview of all its features is outside of scope of this document and you can find out more [here](https://support.phrase.com/hc/en-us/categories/4930564750748-Phrase-Strings).

Here's the description of the processes/steps that you will be doing once and that you will be doing regularly:

## Once/occasionally

### Set up Phrase account and a project

* Sign up on Phrase Strings
* Create a project
* Create an API key

### Set up Phrase plugin

* Install the plugin
* Create Phrase provider asset
* Connect to Phrase using your API key
* Choose the project
* Create the missing locales
* Choose which locales you want to push and which to pull: one possible approach is to maintain the source locale in Unity and push it to Phrase, and pull the target locales from Phrase to Unity when they are translated
* Connect the string tables

## Regularly

* Push translations to Phrase
  * Add metadata such as description and maximum character length
* Pull translations from Phrase to your application
* Attach screenshots
