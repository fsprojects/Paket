# Git dependencies

[This feature is only available in Paket 3.0 prereleases]

Paket allows you to automatically manage the linking of files from any git repo.


This feature assumes that you have [git](https://git-scm.com/) installed.
If you don't have git installed then Paket still allows you to [reference files from github](github-dependencies.html).

## Referencing a Git repository

You can also reference a complete git repository by specifying the clone url in the [`paket.dependencies` file](dependencies-file.html):

    git https://github.com/fsprojects/Paket.git
    git git@github.com:fsharp/FAKE.git

This will download the latest version of the default branch of given repositories and put it into your `paket-files` folder.

If you want to restrict Paket to a special branch or a concrete commit then this is also possible:

    git https://github.com/fsprojects/Paket.git master
    git http://github.com/forki/AskMe.git 97ee5ae7074bdb414a3e5dd7d2f2d752547d0542
