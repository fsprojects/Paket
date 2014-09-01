FAQ - Frequently Asked Questions
================================

Resolving dependencies from NuGet is really slow
------------------------------------------------

Q: When I resolve the dependencies from NuGet.org it is really slow. Why is that?

A: Paket uses the NuGet ODATA API to discover package dependencies. Unfortunately this AAI is very slow. 
Good news is that the NuGet is currently developing a faster API. Paket might take advantage of that.