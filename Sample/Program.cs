
using T4Happiness;


var template = new SimpleTemplate();
//The parameters that were specified in the template file are generated as properties on the template class.
template.SomeText = "some text";
var text = template.TransformText();
Console.WriteLine(text);

//results in "Hello World from 'some text'!" being written to the console
