# Search Extensions

## Disclaimer: this package is full of prototypes, samples and tech demos. It is NOT an official product. You are using it, at your own risk

This package contains a bunch of tools, examples, samples and queries to be used with Unity Search.

The Search Extensions package will work with Unity 2020.3 and any other versions higher than 2021.2.
- If you are using 20.3: also install the `com.unity.quicksearch@3.0.0-preview.22` package.
- If you are using 21.2+ : Search extensions will use the built-in Search framework.

## Extensions Package Installation

There are 2 ways to install the package. Either download the code and embed it in your project or use the Package Manager *Install from git url* feature.

### Download the code

![installation](package/Documentation~/images/installation.png)

1) Press Download code button
2) Unzip the code into a folder (ex: `com.unity.search.extensions-main`)
3) Copy this folder into the `Packages` folder of your project:
![local package](package/Documentation~/images/installation_copy_local_package.png)

### Install from Git URL

1) Open the Package Manager
2) Press the `+` icon and select *Add package from git URL*
![local package](package/Documentation~/images/installation_add_git_url.png)
3) Paste the following URL: `https://github.com/Unity-Technologies/com.unity.search.extensions.git?path=package`
4) Press the `Add` button.

You can now validate that the Dependency Viewer is available in the Window->Search menu:
![local package](package/Documentation~/images/search_menu_dependency_viewer.png)

## Dependency Viewer

This package contains a prototype for a Dependency Viewer. More details can be found in our documentation [wiki](https://github.com/Unity-Technologies/com.unity.search.extensions/wiki/dependency-viewer).

## Query Package Installation
This package contains some good query examples and shows some ready to user `Data Explorer` built using a Table View.

### Install from Unity Asset package
If you want copy in your project all Query Examples you can download the following `.unitypackage`:

[Query examples](https://github.com/Unity-Technologies/com.unity.search.extensions/projects/SearchExtensionsQueries/Assets/queries-examples.unitypackage)

### Install from Git URL
If you want to install a Package in your project containing all the Query Examples:

1) Open the Package Manager
2) Press the `+` icon and select *Add package from git URL*
![local package](package/Documentation~/images/installation_add_git_url.png)
3) Paste the following URL: `https://github.com/Unity-Technologies/com.unity.search.extensions.git?path=package-queries`
4) Press the `Add` button.

If you want to know more about search queries and dynamic collections feel free to read these articles:

- Search Window [Workflows](https://github.com/Unity-Technologies/com.unity.search.extensions/wiki/Search-Features-Walkthrough)
- Dynamic [Collections](https://github.com/Unity-Technologies/com.unity.search.extensions/wiki/Collection-Tool)
- Search [Integration](https://github.com/Unity-Technologies/com.unity.search.extensions/wiki/Search-Integration)
    - Find References [in project](https://github.com/Unity-Technologies/com.unity.search.extensions/wiki/Search-Integration#finding-references-in-232233)
    - Find from [properties](https://github.com/Unity-Technologies/com.unity.search.extensions/wiki/Search-Integration#finding-properties-in-233)

## Disclaimer

*This repository is read-only. Owners won't accept pull requests, GitHub review requests, or any other GitHub-hosted issue management requests.*
