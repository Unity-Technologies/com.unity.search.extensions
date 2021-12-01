# Search Extensions

This package contains a bunch of tools, examples, samples and queries to be used with Unity Search.

The Search Extensions package will work with Unity 2020.3 and any other versions higher than 2021.2.
- If you are using 20.3: also install the `com.unity.quicksearch@3.0.0-preview.22` package.
- If you are using 21.2+ : Search extensions will use the built-in Search framework.

## Package Installation

There are 2 ways to install the package. Either download the code and embed it in your project or use the Package Manager *Install from git url* feature.

### If you are on 20.3

You first need to install the latest preview version of the QuickSearch package. 

1) Go to menu Edit->Project Settings. Select Package Manager and enable *Preview Package*.
![preview package](package/Documentation~/images/installation_package_manager_enable_preview.png)
2) Open Package Manager (Window->Package Manager menu). Search the registry for QuickSearch and select the latest preview version of QuickSearch 3.0 (preview.17 at the time this is written).
![preview package](package/Documentation~/images/installation_package_manager_download_quicksearch_preview.png)

### If you are on 21.2 and more

[Search](https://docs.unity3d.com/2021.2/Documentation/Manual/search-overview.html) is already available as a core feature. You just need to properly install the search-extensions package.

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

Note that the Dependency Viewer works only for:

- **Unity 2020.3**
- **Unity 2021.2 and more**

## Disclaimer

*This repository is read-only. Owners won't accept pull requests, GitHub review requests, or any other GitHub-hosted issue management requests.*
