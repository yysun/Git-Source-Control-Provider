Git Source Control Provider
===========================

Introduction
------------
Visual Studio users are used to see file status of source control right inside the solution explorer, whether it is SourceSafe, Team Foundation Server, Subversion or even Mercurial. This plug-in integrates Git with Visual Studio solution explorer. It supports all editions of Visual studio 2010 except the Express Edition.

![solution explorer](http://gitscc.codeplex.com/Project/Download/FileDownload.aspx?DownloadId=123874)

Features
--------
* Display file status in solution explorer and solution navigator
* Display repository status e.g. in the middle of merging, patching, rebase and bisecting
* Enable/disable plug-in through visual studio's source control plug-in selection
* No source code control information stored in solution or project file
* Initialize new git repository and generate .gitignore 
* Integrates with [Git for Windows](http://code.google.com/p/msysgit)
* Integrates with [Git Extensions](http://code.google.com/p/gitextensions)
* Integrates with [TortoiseGit](http://code.google.com/p/tortoisegit)
* [Git - Pending Changes Tool Window] (http://gitscc.codeplex.com/wikipage?title=Commit%20Changes)
* [Git - View History Tool Window] (http://gitscc.codeplex.com/wikipage?title=View%20History)
* Options page

How to use
----------
* Install [Git for Windows](http://code.google.com/p/msysgit), or [Git Extensions](http://code.google.com/p/gitextensions), or [TortoiseGit](http://code.google.com/p/tortoisegit).
* Run Visual Studio. 
* Go to Tools | Extension Manager, search online gallery for Git Source Control Provider and install. 
* Go to Tools | Options, Select Source Control.
* Select Git Source Control Provider from the drop down list, and click OK.
* Open your solution controlled by Git to see the file's status.
* Right click within solution explorer and select "Git". If Git for Windows, Git Extensions or TortoiseGit are installed, their commands are listed in the menu.
* Using the option page to disable the commands if you like.

Documentation
-----------------
[Documentation](http://gitscc.codeplex.com/documentation)
[Change Logs](http://gitscc.codeplex.com/releases/view/46589)
[Project Roadmap](http://gitscc.codeplex.com/wikipage?title=Project%20Roadmap)
