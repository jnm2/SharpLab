using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Diagnostics.Runtime;
using SharpLab.Runtime.Internal;

public static class Inspect {
    private static ClrRuntime _runtime;

    public static void Heap(object @object) {
        EnsureRuntime();

        var address = (ulong)GetHeapPointer(@object);
        var objectType = _runtime.Heap.GetObjectType((ulong)GetHeapPointer(@object));
        if (objectType == null)
            throw new Exception($"Failed to find object type for address 0x{(ulong)GetHeapPointer(@object):X}.");

        var objectSize = objectType.GetSize(address);

        // Move by one pointer size back -- Object Header,
        // see https://blogs.msdn.microsoft.com/seteplia/2017/05/26/managed-object-internals-part-1-layout/
        //
        // Not sure if there is a better way to get this through ClrMD yet.
        var objectStart = address - (uint)IntPtr.Size;
        var data = ReadMemory(objectStart, objectSize);

        var sourceFields = objectType.Fields;
        var sourceFieldCount = objectType.Fields.Count;
        var fields = new MemoryInspectionResult.Field[sourceFieldCount];
        for (var i = 0; i < sourceFieldCount; i++) {
            var sourceField = sourceFields[i];
            var offset = (int)(sourceField.GetAddress(address) - objectStart);
            fields[i] = new MemoryInspectionResult.Field(
                sourceField.Name,
                offset,
                sourceField.Size
            );
        }

        Output.Write(new MemoryInspectionResult(address, objectType.Name, fields, data));
    }

    private static byte[] ReadMemory(ulong address, ulong size) {
        var data = new byte[size];
        _runtime.ReadMemory(address, data, (int)size, out var _);
        return data;
    }

    private static unsafe IntPtr GetHeapPointer(object @object) {
        var indirect = Unsafe.AsPointer(ref @object);
        return **(IntPtr**)(&indirect);
    }

    private static void EnsureRuntime() {
        if (_runtime != null)
            return;
        var dataTarget = DataTarget.AttachToProcess(InspectionSettings.CurrentProcessId, UInt32.MaxValue, AttachFlag.Passive);
        _runtime = dataTarget.ClrVersions.Single().CreateRuntime();
    }

    //public static unsafe void Stack() {
    //    byte* stackEnd = stackalloc byte[1];
    //    var stackEndCutoff = (ulong)stackEnd;

    //    using (var dataTarget = DataTarget.AttachToProcess(InspectionSettings.CurrentProcessId, UInt32.MaxValue, AttachFlag.Passive)) {
    //        var runtime = dataTarget.ClrVersions.Single().CreateRuntime();
    //        var thread = FindCurrentThread(runtime);
    //        var builder = new StringBuilder();
    //        var stackStartCutoff = InspectionSettings.StackStart;
    //        foreach (var value in thread.EnumerateStackObjects()) {
    //            if (value.Address > stackStartCutoff || value.Address < stackEndCutoff)
    //                continue;

    //            builder
    //                .AppendFormat("0x{0:X}", value.Address)
    //                .Append(" ")
    //                .Append(value.Kind)
    //                .Append(" ")
    //                .Append(value.Type.Name)
    //                .AppendLine();
    //        }
    //        Output.Write(new SimpleInspectionResult("Stack", builder));
    //    }
    //}

    //private static ClrThread FindCurrentThread(ClrRuntime runtime) {
    //    var managedThreadId = Thread.CurrentThread.ManagedThreadId;
    //    foreach (var thread in runtime.Threads) {
    //        if (thread.ManagedThreadId == managedThreadId)
    //            return thread;
    //    }

    //    throw new Exception($"Could not find thread {managedThreadId}.");
    //}

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static new bool Equals(object a, object b) {
        throw new NotSupportedException();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new bool ReferenceEquals(object objA, object objB) {
        throw new NotSupportedException();
    }
}