$(function () {
    var $button = $('#button');
    var $key = $('#key');
    //var $checkbox = $('checkbox');
    $button.click(function () {
        if (checkbox.checked == false) {
            alert("请仔细阅读并同意用户协议")
        }
        else {
            var phoneNumber = $('#phone').val();
            var psw = $('#psw').val();
            var pswagain = $('#pswagain').val();
            var code = $('#code').val();
            if (psw == pswagain & psw != 0) {
                $.post("../Account/Signup",
                    {
                        PhoneNumber: phoneNumber,
                        Code: code,
                        Password: psw,
                        PasswordAga: pswagain
                    },
               function (data) {
                   if (data.Succeeded == true) {
                       $.post("../Account/query",
                           {
                               phoneNumber: phone
                           },
                           function (datas) {
                               if (datas.Succeeded == true)
                                   //alert("恭喜您，注册成功！");
                                   window.location.href = '../Account/Success';
                               else
                                   alert(datas.ErrorMessage);
                           })
                   }
                   else
                       alert(data.ErrorMessage);
               })
            }
            else {
                { alert("密码输入错误,请重新填写！"); }
            }
        }
    })
    $key.click(function () {
        var phoneNumber = $('#phone').val();
        $.post("../Account/SendSMS",
            { phoneNumber: phoneNumber }
        ,
        function (data) {
            if (data.Succeeded == false)
                alert(data.ErrorMessage);
        })
    })
})