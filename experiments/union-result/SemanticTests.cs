using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace UnionResultLab;
public static class SemanticTests
{
    public static object Run()
    {
        int assertions=0;
        void Check(bool condition,string message) { assertions++; if(!condition) throw new Exception(message); }
        void Throws<T>(Action action) where T:Exception
        { try { action(); } catch(T) { assertions++; return; } throw new Exception("Expected "+typeof(T).Name); }
        var distributions=new List<object>();
        foreach(int percentage in new[]{50,95})
        {
            var data=new Inputs(percentage); long expectedSum=0;
            var sums=new long[8];
            for(int i=0;i<Inputs.Count;i++)
            {
                int expected=data.Success[i]?data.Values[i]:-data.Values[i]; expectedSum+=expected;
                var ru=Operations.CreateReferenceUnion(data.Success[i],data.Values[i]);
                var rr=Operations.CreateReferenceResult(data.Success[i],data.Values[i]);
                var vu=Operations.CreateValueUnion(data.Success[i],data.Values[i]);
                var vr=Operations.CreateValueResult(data.Success[i],data.Values[i]);
                int[] outputs=[Operations.HandleReferenceUnion(in ru),Operations.HandleReferenceResult(in rr),
                    Operations.HandleValueUnion(in vu),Operations.HandleValueResult(in vr),
                    Operations.HandleReferenceUnion(in data.ReferenceUnions[i]),Operations.HandleReferenceResult(in data.ReferenceResults[i]),
                    Operations.HandleValueUnion(in data.ValueUnions[i]),Operations.HandleValueResult(in data.ValueResults[i])];
                for(int j=0;j<outputs.Length;j++) { Check(outputs[j]==expected,"Per-input parity"); sums[j]+=outputs[j]; }
                Check(rr.Kind!=OutcomeKind.Invalid && vr.Kind!=OutcomeKind.Invalid,"Factory tag");
                Check(ReferenceEquals(data.ReferenceUnions[i].Value,data.Success[i]?data.ReferenceResults[i].Accepted:(object?)data.ReferenceResults[i].Rejected),"Reference identity");
                Check((ResponseAdapter.From(in ru)==ResponseAdapter.From(in rr)),"HTTP DTO parity");
            }
            Check(sums.All(x=>x==expectedSum),"Aggregate parity");
            Check(data.Success.Count(x=>x)==Inputs.Count*percentage/100,"Distribution");
            distributions.Add(new{percentage,successes=data.Success.Count(x=>x),count=Inputs.Count,data.SequenceHash,expectedSum,sums});
        }
        ReferenceOutcome emptyReference=default; ValueOutcome emptyValue=default;
        ReferenceResult emptyReferenceResult=default; ValueResult emptyValueResult=default;
        Check(emptyReference.Value is null && emptyValue.Value is null,"Union defaults are empty");
        Throws<InvalidOperationException>(()=>Operations.HandleReferenceUnion(in emptyReference));
        Throws<InvalidOperationException>(()=>Operations.HandleValueUnion(in emptyValue));
        Throws<InvalidOperationException>(()=>Operations.HandleReferenceResult(in emptyReferenceResult));
        Throws<InvalidOperationException>(()=>Operations.HandleValueResult(in emptyValueResult));
        Throws<ArgumentNullException>(()=>ReferenceResult.Success(null!));
        Throws<ArgumentNullException>(()=>ReferenceResult.Failure(null!));
        ReferenceOutcome nullCase=new((AcceptedRef)null!);
        Check(nullCase.Value is null,"Null case stays empty");
        Throws<InvalidOperationException>(()=>Operations.HandleReferenceUnion(in nullCase));
        var wire=new List<object>();
        var nativeSerialization=new List<object>();
        foreach(bool success in new[]{true,false})
        {
            var union=Operations.CreateReferenceUnion(success,730031);
            var result=Operations.CreateReferenceResult(success,730031);
            string a=JsonSerializer.Serialize(ResponseAdapter.From(in union));
            string b=JsonSerializer.Serialize(ResponseAdapter.From(in result));
            Check(a==b,"Identical wire bytes");
            Check(ResponseAdapter.Validate(JsonSerializer.Deserialize<HttpOutcome>(a))==ResponseAdapter.From(in union),"Wire round trip");
            wire.Add(new{success,json=a});
            string native=JsonSerializer.Serialize(union);
            var valueUnion=Operations.CreateValueUnion(success,730031);
            string nativeValue=JsonSerializer.Serialize(valueUnion);
            string expectedNative=success ? "{\"CourseId\":730031}" : "{\"ReasonCode\":730031}";
            Check(native==expectedNative && nativeValue==expectedNative,"Native active-case serialization without discriminator");
            nativeSerialization.Add(new{success,reference=native,value=nativeValue});
        }
        foreach(string json in new[]{"null","{}","{\"StatusCode\":200,\"Value\":1}","{\"StatusCode\":200,\"Kind\":\"unknown\",\"Value\":1}","{\"StatusCode\":422,\"Kind\":\"accepted\",\"Value\":1}"})
            Throws<ArgumentException>(()=>ResponseAdapter.Validate(JsonSerializer.Deserialize<HttpOutcome>(json)));
        PrimitiveOutcome number=new(42); PrimitiveOutcome text=new("sample");
        Check(JsonSerializer.Serialize(number)=="42","Native number JSON");
        Check(JsonSerializer.Serialize(text)=="\"sample\"","Native string JSON");
        Check(JsonSerializer.Deserialize<PrimitiveOutcome>("42").Value is int n && n==42,"Native number round trip");
        Check(JsonSerializer.Deserialize<PrimitiveOutcome>("\"sample\"").Value is string s && s=="sample","Native string round trip");
        Check(JsonSerializer.Serialize(default(PrimitiveOutcome))=="null","Default primitive JSON");
        Check(JsonSerializer.Deserialize<PrimitiveOutcome>("null").Value is null,"Null union JSON");
        Check(JsonSerializer.Deserialize<ReferenceOutcome>("null").Value is null,"Null reference union JSON");
        Check(JsonSerializer.Serialize(default(ReferenceOutcome))=="null","Default reference union serialization");
        Check(JsonSerializer.Serialize(default(ValueOutcome))=="null","Default value union serialization");
        var nativeObjects=new List<object>();
        foreach(string json in new[]{"{\"CourseId\":730031}","{\"ReasonCode\":17}","{}","{\"Unknown\":1}"})
        {
            try { var x=JsonSerializer.Deserialize<ReferenceOutcome>(json); nativeObjects.Add(new{json,resultType=x.Value?.GetType().FullName,error=(string?)null}); }
            catch(Exception ex) { nativeObjects.Add(new{json,resultType=(string?)null,error=ex.GetType().FullName,message=ex.Message}); }
        }
        object Layout<T>() => new{type=typeof(T).FullName,size=Unsafe.SizeOf<T>(),fields=typeof(T).GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Select(f=>new{name=f.Name,type=f.FieldType.FullName}),constructors=typeof(T).GetConstructors().Select(c=>new{signature=c.ToString(),il=Convert.ToHexString(c.GetMethodBody()?.GetILAsByteArray()??[])})};
        return new{status="PASS",assertions,distributions,wire,nativeSerialization,nativeObjects,
            jsonScope="Reflection serializer; explicit reference-family DTO byte parity and DTO validation only. No reverse domain adapter, value-family DTO adapter, classifier behavior, source generation or AOT claim.",
            layouts=new[]{Layout<ReferenceOutcome>(),Layout<ReferenceResult>(),Layout<ValueOutcome>(),Layout<ValueResult>(),Layout<AcceptedValue>(),Layout<RejectedValue>()},
            arrayPayloadBytes=new{referenceUnion=Inputs.Count*Unsafe.SizeOf<ReferenceOutcome>(),referenceResult=Inputs.Count*Unsafe.SizeOf<ReferenceResult>(),valueUnion=Inputs.Count*Unsafe.SizeOf<ValueOutcome>(),valueResult=Inputs.Count*Unsafe.SizeOf<ValueResult>()},
            objectArrayFootprintCaveat="Array payload only; headers and separately allocated records/boxes excluded."};
    }
}
