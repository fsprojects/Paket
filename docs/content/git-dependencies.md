# Git dependencies

[This feature is only available in Paket 3.0 prereleases]

Paket allows you to automatically manage the linking of files from any git repo.
<b>This feature assumes that you have [git](https://git-scm.com/) installed.</b>
If you don't have git installed then Paket still allows you to reference files from [github](github-dependencies.html).

## Referencing a Git repository

You can also reference a complete git repository by specifying the clone url in the [`paket.dependencies` file](dependencies-file.html):

    git https://github.com/fsprojects/Paket.git
	git git@github.com:fsharp/FAKE.git

This will download the given repository and put it into your `paket-files` folder.