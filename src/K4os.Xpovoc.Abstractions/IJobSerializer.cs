namespace K4os.Xpovoc.Abstractions;

public interface IJobSerializer
{
	string Serialize(object job);
	object Deserialize(string payload);
}