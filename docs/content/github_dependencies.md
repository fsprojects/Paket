# GitHub dependencies

Paket allows to link files from [github.com](http://www.github.com) into your projects.

## Referencing a single file

You can reference a single file from [github.com](http://www.github.com) simply by specifying the source repository and the file name in the [`paket.dependencies` file](dependencies_file.html):

    github forki/FsUnit FsUnit.fs

If you run the [`paket update` command](paket_update.html), then it will add a new section to your [`paket.lock` file](lock_file.html):

    [lang=batchfile]
	GITHUB
	  remote: forki/FsUnit
	  specs:
		FsUnit.fs (7623fc13439f0e60bd05c1ed3b5f6dcb937fe468)

As you can see the file is pinned to a concrete commit. This allows you to reliably use the same file version in succeeding builds.

If you want to reference the file in one of your project files then add an entry to the project's [`paket.references` file.](references_files.html):

	[lang=batchfile]
	File:FsUnit.fs

This will reference the linked file directly into your project.

![alt text](img/github_reference.png "GitHub file referenced in project")