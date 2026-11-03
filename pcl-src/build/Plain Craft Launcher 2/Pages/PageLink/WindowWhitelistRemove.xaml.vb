Public Class WindowWhitelistRemove

    Private _Checks As New List(Of KeyValuePair(Of String, MyCheckBox))

    '''<summary>弹出勾选窗口，返回被勾选的名字；取消返回空列表。</summary>
    Public Shared Function Pick(Names As List(Of String)) As List(Of String)
        Dim W As New WindowWhitelistRemove
        For Each N In Names
            Dim C As New MyCheckBox With {.Text = N, .Height = 26}
            W.PanList.Children.Add(C)
            W._Checks.Add(New KeyValuePair(Of String, MyCheckBox)(N, C))
        Next
        If W._Checks.Count = 0 Then Return New List(Of String)
        AddHandler W.BtnOk.Click, Sub(s, e) W.DialogResult = True
        AddHandler W.BtnCancel.Click, Sub(s, e) W.DialogResult = False
        If W.ShowDialog() = True Then
            Dim Result As New List(Of String)
            For Each P In W._Checks
                If P.Value.Checked Then Result.Add(P.Key)
            Next
            Return Result
        End If
        Return New List(Of String)
    End Function

End Class