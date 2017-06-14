## Sample

Running the command:

    [lang=batchfile]
    paket find-packages

and then enter search text and press enter:

![alt text](img/paket-find-packages.png "paket find-packages command")

## Silent mode

The silent mode can be used for additional tooling support in various editors. It allows to create suggestions for [`paket add`](paket-add.html):

    [lang=batchfile]
    paket find-packages -s

The command allows runs to suggest package names. It will keep running in a loop until it receives the text ":q".
