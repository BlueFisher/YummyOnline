$(function () {
    var $btn = $('#btn');
    $btn.click(function () {
        $.ajax({
            url: "../select",
            type: "POST",
            datatype: "json",
            success: function (data) {
                console.log(data);
                var $dom = $('#dinelist');
                $dom.children().remove();
                if (data.Data.Data.length==0)
                    $("<p>暂无历史订单</p>").appendTo("#dinelist");
                else
                    data.Data.Data.forEach(function (x) {
                        console.log(x.BeginTime)

                        $("订单号: " + x.Id + "<br/>金额: " + x.Price + "</p>").appendTo("#dinelist")
                    })
            }
        })
    })
    $btn.click();
    function formatDate(now) {
        console.log(now)
        var year = now.getFullYear();
        var month = now.getMonth() + 1;
        var date = now.getDate();
        var hour = now.getHours();
        var minute = now.getMinutes();
        var second = now.getSeconds();
        return year
                + "-"
                + (month.toString().length == 1 ? "0" + month : month)
                + "-"
                + (date.toString().length == 1 ? "0" + date : date) + " " + hour + ":" + minute + ":" + second;
    }
})