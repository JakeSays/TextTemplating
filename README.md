This is a simple T4 text template preprocessor source generator. It is based on the T4 parsing code from [Mono's T4 project](https://github.com/mono/t4).

You can find the source to the generator here: [JakeSays/TextTemplating](https://github.com/JakeSays/TextTemplating)
                                                                     
    Note that the generator currently only supports C#.

To use the generator first add the .nuget package to your C# project. Then add a T4 template source file, or use an existing one.

Change the build action of the template file to `AdditionalFiles`, no custom tool.

Now any time you make a change to the T4 file the generator will automatically generate a preprocessed class.




