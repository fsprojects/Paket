## Example

Consider the following [`paket.dependencies` file](dependencies-file.html) file:

```paket
source https://nuget.org/api/v2

framework: net40

nuget Castle.Core
nuget Castle.Windsor
```

and the following [`paket.lock` file](lock-file.html):

```paket
RESTRICTION: == net40
NUGET
  remote: https://nuget.org/api/v2
    Castle.Core (2.0.0)
    Castle.Windsor (2.0.0)
      Castle.Core (>= 2.0.0)
```

Now we run `paket outdated`:

```sh
$ paket outdated
Paket version 5.0.0
Resolving packages for group Main:
 - Castle.Core 4.1.0
 - Castle.Windsor 3.4.0
   Incompatible dependency: Castle.Core >= 3.3 < 4.0 conflicts with resolved version 4.1.0
 - Castle.Windsor 3.3.0
Outdated packages found:
  Group: Main
    * Castle.Core 2.0.0 -> 4.1.0
    * Castle.Windsor 2.0.0 -> 3.3.0
Performance:
 - Resolver: 3 seconds (1 runs)
    - Runtime: 199 milliseconds
    - Blocked (retrieving package details): 1 second (3 times)
    - Blocked (retrieving package versions): 1 second (1 times)
    - Not Blocked (retrieving package versions): 1 times
 - Average Request Time: 884 milliseconds
 - Number of Requests: 12
 - Runtime: 4 seconds
```
