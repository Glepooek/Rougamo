# Rougamo - �����

### rougamo��ʲô
��̬����֯��AOP��.NET��õ�AOPӦ����Castle DynamicProxy��rougamo�Ĺ����������ƣ�����ʵ��ȴ��Ȼ��ͬ��
DynamicProxy������ʱ����һ�������࣬ͨ��������д�ķ�ʽִ��֯����룬rougamo���Ǵ������ʱֱ���޸�IL���룬
.NET��̬AOP������һ���ܺõ����PostSharp��rougamo��ע�뷽ʽҲ���������Ƶġ�

### ���ٿ�ʼ(MoAttribute)
```csharp
// 1.NuGet����Rougamo.Fody
// 2.������̳�MoAttribute��ͬʱ������Ҫ֯��Ĵ���
public class LoggingAttribute : MoAttribute
{
    public override void OnEntry(MethodContext context)
    {
        // ��context��������ȡ��������Ρ���ʵ����������������Ϣ
        Log.Info("����ִ��ǰ");
    }

    public override void OnException(MethodContext context)
    {
        Log.Error("����ִ���쳣", context.Exception);
    }

    public override void OnSuccess(MethodContext context)
    {
        Log.Info("����ִ�гɹ���");
    }

    public override void OnExit(MethodContext context)
    {
        Log.Info("�����˳�ʱ�����۷���ִ�гɹ������쳣������ִ��");
    }
}

// 3.Ӧ��Attribute
public class Service
{
    [Logging]
    public static int Sync(Model model)
    {
        // ...
    }

    [Logging]
    public async Task<Data> Async(int id)
    {
        // ...
    }
}
```

##### ���ݷ����ɷ���������Ӧ��
�ڿ��ٿ�ʼ�н�������ν�����֯�뵽ָ�������ϣ���ʵ��ʹ��ʱ��һ���Ӵ����Ŀ���ÿ��������ܶ෽����ȥ�������Attribute
���ܻ�ܷ���������`MoAttribute`���Ϊ����Ӧ���ڷ���(method)����(class)������(assembly)��ģ��(module)��ͬʱ������
�ɷ��������ԣ����������
```csharp
// 1.�ڼ̳�MoAttribute��ͬʱ����дFlags���ԣ�δ��дʱĬ��InstancePublic(publicʵ������)
public class LoggingAttribute : MoAttribute
{
    // ��Ϊ����public������Ч��������ʵ���������Ǿ�̬����
    public override AccessFlags Flags => AccessFlags.Public;

    // ������дʡ��
}

// 2.Ӧ��
// 2.1.Ӧ��������
[Logging]
public class Service
{
    // ��̬֯�뽫��Ӧ��
    public static void M1() { }

    // ��̬֯�뽫��Ӧ��
    public void M2() { }

    // ����Ӧ�þ�̬֯��
    protected void M3() { }
}
// 2.2.Ӧ���ڳ����ϣ��ó�������public��������Ӧ�þ�̬֯��
[assembly: Logging]
```

### �쳣������޸ķ���ֵ��v1.1.0��
��`OnException`�����п���ͨ������`MethodContext`��`HandledException`���������쳣�Ѵ������÷���ֵ��
��`OnSuccess`�����п���ͨ������`MethodContext`��`ReplaceReturnValue`�����޸ķ���ʵ�ʵķ���ֵ����Ҫע����ǣ�
��Ҫֱ��ͨ��`ReturnValue`��`ExceptionHandled`����Щ�������޸ķ���ֵ�ʹ����쳣��`HandledException`��
`ReplaceReturnValue`����һЩ�����߼����������ܻ�����¡�
```csharp
public class TestAttribute : MoAttribute
{
    public override void OnException(MethodContext context)
    {
        // �����쳣��������ֵ����ΪnewReturnValue����������޷���ֵ(void)��ֱ�Ӵ���null����
        context.HandledException(this, newReturnValue);
    }

    public override void OnSuccess(MethodContext context)
    {
        // �޸ķ�������ֵ
        context.ReplaceReturnValue(this, newReturnValue);
    }
}
```

### ����֯��(IgnoreMoAttribute)
�ڿ��ٿ�ʼ�У����ǽ������������Ӧ�ã������������õĹ���ֻ�޶��˷����ɷ����ԣ����Կ�����Щ���Ϲ���ķ���������Ӧ��֯�룬
��ʱ���ʹ��`IgnoreMoAttribute`��ָ������/����б�ǣ���ô�÷���/��(�����з���)��������֯�롣�����`IgnoreMoAttribute`
Ӧ�õ�����(assembly)��ģ��(module)����ô�ó���(assembly)/ģ��(module)����������֯�롣���⣬��Ӧ��`IgnoreMoAttribute`
ʱ������ͨ��MoTypesָ�����Ե�֯�����͡�
```csharp
// ��ǰ���򼯺�������֯��
[assembly: IgnoreMo]
// ��ǰ���򼯺���TheMoAttribute��֯��
[assembly: IgnoreMo(MoTypes = new[] { typeof(TheMoAttribute))]

// ��ǰ���������֯��
[IgnoreMo]
class Class1
{
    // ...
}

// ��ǰ�����TheMoAttribute��֯��
[IgnoreMo(MoTypes = new[] { typeof(TheMoAttribute))]
class Class2
{
    // ...
}
```

### ͨ��ʵ�ֿսӿڵķ�ʽ����֯��(IRougamo<>)
���ÿ����������Attribute��Ǹо�̫����������ͨ�������ɷ����Խ�������֯���ֲ����Զ��壬��ô�÷�ʽ�����������������
```csharp
// 1.������Ҫ֯��Ĵ��룬Ҳ����ֱ��ʹ�ÿ��ٿ�ʼ�ж����LoggingAttribute
public class LoggingMo : IMo
{
    public override AccessFlags Flags => AccessFlags.All;

    public override void OnEntry(MethodContext context)
    {
        // ��context��������ȡ��������Ρ���ʵ����������������Ϣ
        Log.Info("����ִ��ǰ");
    }

    public override void OnException(MethodContext context)
    {
        Log.Error("����ִ���쳣", context.Exception);
    }

    public override void OnExit(MethodContext context)
    {
        Log.Info("�����˳�ʱ�����۷���ִ�гɹ������쳣������ִ��");
    }

    public override void OnSuccess(MethodContext context)
    {
        Log.Info("����ִ�гɹ���");
    }
}

// 2.����սӿ�
public interface ILoggingRougamo : IRougamo<LoggingMo>
{
}

// 3.Ӧ�ÿսӿڣ��������ʱϰ�߽�ͬһ����/������ඨ��һ�����ӿ�/���࣬��ôֻ��Ҫ���ӿ�/����ʵ�ָýӿڼ���
public interface IRepository<TModel, TId> : ILoggingRougamo
{
    // ...
}
```

### ֯�뻥��
##### �����ͻ���(IRougamo<,>)
����������Attribute��Ǻͽӿ�ʵ������֯�뷽ʽ����ô�Ϳ��ܳ���ͬʱӦ�õ���������������֯�����������ͬ�ģ��Ǿͻ����
�ظ�֯��������Ϊ�˾�����������������ڽӿڶ���ʱ�����Զ��廥�����ͣ�Ҳ����ͬʱֻ��һ������Ч�������ĸ���Ч������
[���ȼ�](#Priority)����
```csharp
public class Mo1Attribute : MoAttribute
{
    // ...
}
public class Mo2Attribute : MoAttribute
{
    // ...
}
public class Mo3Attribute : MoAttribute
{
    // ...
}

public class Test : IRougamo<Mo1Attribute, Mo2Attribute>
{
    [Mo2]
    public void M1()
    {
        // Mo2AttributeӦ���ڷ����ϣ����ȼ����ڽӿ�ʵ�ֵ�Mo1Attribute��Mo2Attribute����Ӧ��
    }

    [Mo3]
    public void M2()
    {
        // Mo1Attribute��Mo3Attribute�����⣬����������Ӧ��
    }
}
```
##### �����ͻ���(IRepulsionsRougamo<,>)
`IRougamo<,>`ֻ����һ�����ͻ��⣬`IRepulsionsRougamo<,>`������������ͻ���
```csharp
public class Mo1Attribute : MoAttribute
{
}
public class Mo2Attribute : MoAttribute
{
}
public class Mo3Attribute : MoAttribute
{
}
public class Mo4Attribute : MoAttribute
{
}
public class Mo5Attribute : MoAttribute
{
}

public class TestRepulsion : MoRepulsion
{
    public override Type[] Repulsions => new[] { typeof(Mo2Attribute), typeof(Mo3Attribute) };
}

[assembly: Mo2]
[assembly: Mo5]

public class Class2 : IRepulsionsRougamo<Mo1Attribute, TestRepulsion>
{
    [Mo3]
    public void M1()
    {
        // Mo1��Mo2��Mo3���⣬������Mo3���ȼ�����Mo1������Mo1����Чʱ�����л������Ͷ�����Ч
        // ��������Mo2Attribute��Mo3Attribute��Mo5Attribute����Ӧ��
        Console.WriteLine("m1");
    }

    [Mo4]
    public void M2()
    {
        // Mo1��Mo2��Mo3���⣬������Mo1���ȼ�����Mo2������Mo2������Ч
        // ����Mo1Attribute��Mo4Attribute��Mo5Attribute����Ӧ��
        Console.WriteLine("m2");
    }
}
```
<font color=red>ͨ����������ӣ������ע�⵽����������ͻ��Ⲣ���Ƕ�����֮�以�໥�⣬���ǵ�һ��������ڶ������Ͷ�������ͻ��⣬
�ڶ�������֮�䲢�����⣬Ҳ���������ʾ����������`Mo1Attribute`����Чʱ�����������`Mo2Attribute`��`Mo3Attribute`������Ч��
������Ҫ��⣬���廥���ԭ����Attribute�Ϳսӿ�ʵ�����ַ�ʽ���ܴ��ڵ��ظ�Ӧ�ã���������Ϊ���ų�����֯����ظ���ͬʱҲ���Ƽ�ʹ��
�໥�ⶨ�壬�������׳����߼����ң�������Ӧ��֯��ǰ��ϸ˼��һ��ͳһ�Ĺ��򣬶��������ⶨ�壬Ȼ����ͼʹ�ö໥�����������</font>

### Attribute����֯��(MoProxyAttribute)
������Ѿ�ʹ��һЩ�����������һЩ����������Attribute��ǣ�������ϣ������Щ��ǹ��ķ�������aop���������ֲ���һ��һ���ֶ�����rougamo
��Attribute��ǣ���ʱ�����ͨ������ķ�ʽһ�����aop֯�롣�ٱ��������Ŀ�����кܶ�����`ObsoleteAttribute`�Ĺ�ʱ��������ϣ��
�ڹ��ڷ����ڱ�����ʱ������ö�ջ��־�������Ų�������Щ�����ʹ����Щ���ڷ�����Ҳ����ͨ���÷�ʽ��ɡ�
```csharp
public class ObsoleteProxyMoAttribute : MoAttribute
{
    public override void OnEntry(MethodContext context)
    {
        Log.Warning("���ڷ����������ˣ�" + Environment.StackTrace);
    }
}

[module: MoProxy(typeof(ObsoleteAttribute), typeof(ObsoleteProxyMoAttribute))]

public class Cls
{
    [Obsolete]
    private int GetId()
    {
        // �÷�����Ӧ��֯�����
        return 123;
    }
}
```

### ���ȼ�(Priority)
1. `IgnoreMoAttribute`
2. Method `MoAttribute`
3. Method `MoProxyAttribute`
4. Type `MoAttribute`
5. Type `MoProxyAttribute`
6. Type `IRougamo<>`, `IRougamo<,>`, `IRepulsionsRougamo<,>`
7. Assembly & Module `MoAttribute`

### ����(enable/disable)
Rougamo�ɸ��˿����������������ޣ���IL���о�Ҳ������ô����͸������������.NET�ķ�չҲ�᲻�ϵĳ���һЩ�µ����͡��µ����������µ�ILָ�
Ҳ��˿��ܻ����һЩBUG������IL�����BUG�����޷����ٶ�λ�����Ⲣ�޸������������ṩ��һ�����ؿ����ڲ�ȥ��Rougamo���õ�����²����д���֯�룬
Ҳ����Ƽ���λ��ʹ��Rougamo���д���֯��ʱ��֯��Ĵ����ǲ�Ӱ��ҵ��ģ�������־��APM�����ϣ��ʹ���ȶ��������������ܹ����ٵõ�֧�ֵľ�̬֯��
������Ƽ�ʹ��[PostSharp](https://www.postsharp.net)

Rougamo����[fody](https://github.com/Fody/Fody)�Ļ������з��ģ�����Rougamo���״α��������һ��`FodyWeavers.xml`�ļ���Ĭ����������
```xml
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
  <Rougamo />
</Weavers>
```
��ϣ������Rougamoʱ����Ҫ�������ļ���`Rougamo`�ڵ���������`enabled`������ֵΪ`false`
```xml
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
  <Rougamo enabled="false" />
</Weavers>
```

### ��¼yield return IEnumerable/IAsyncEnumerable����ֵ
����֪����ʹ��`yield return`�﷨��+`IEnumerable`����ֵ�ķ������ڵ��÷��������󣬸÷����Ĵ���ʵ�ʲ�û��ִ�У������ʵ��ִ�����������
���`IEnumerable`�������Ԫ��ʱ�򣬱�����ȥforeach���������ߵ���`ToList/ToArray`��ʱ�򣬲��ҷ��ص���ЩԪ�ز�û��һ������/�������
ͳһ�ı��棨����ԭ�������ﲻչ��˵���ˣ�������Ĭ���������û�а취ֱ�ӻ�ȡ��`yield return IEnumerable`���ص�����Ԫ�ؼ��ϵġ�

��������Щ�Դ����رȽ��ϸ�ĳ�����Ҫ��¼���з���ֵ��������ʵ�����Ҵ�����һ�����鱣�������еķ���Ԫ�أ�����������������Ƕ��ⴴ���ģ���ռ
�ö�����ڴ�ռ䣬ͬʱ�ֲ�������`IEnumerable`���ص�Ԫ�ؼ����ж������Ϊ�˱�����ܶ������������ڴ����ģ�Ĭ��������ǲ����¼
`yield return IEnumerable`����ֵ�ģ������Ҫ��¼����ֵ����Ҫ��`FodyWeavers.xml`��`Rougamo`�ڵ�������������`enumerable-returns="true"`
```xml
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
  <Rougamo enumerable-returns="true" />
</Weavers>
```

### todo
1. ���Դ���
2. Ӣ���ĵ�