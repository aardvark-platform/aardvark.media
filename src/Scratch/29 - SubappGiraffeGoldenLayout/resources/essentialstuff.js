
$.fn.numeric = function (ob, value) {

    this.find("input").each(function () {

        if ("numericsetvalue" in this) {
            var setValue = this.numericsetvalue;
            if (ob === "set") setValue(value, false);
            return;
        }

        var config = { changed: function (v) { } };
        var o = $.extend(config, ob);


        var self = this;
        var old = parseFloat(self.value);

        function largeStep() {
            var s = parseFloat(self.getAttribute("data-largestep"));
            if (isNaN(s)) return 10.0;
            else return s;
        }

        function formatFloat(value, decimal) {
            return value.toPrecision(15)
                .replace(/\.([0-9]*?)0+([eE][\+\-0-9]*$)?$/, ".$1$2")
                .replace(/\.$/, "")
                .replace(/\.([eE])/, "$1")
                .replace("e", "E");
        }

        function setValue(value, raiseEvent) {
            var initial = old;
            try { initial = parseFloat(eval(value)); }
            catch (e) { initial = old; }

            var v = initial;
            var min = parseFloat(self.min);
            var max = parseFloat(self.max);

            if (!isNaN(min) && v < min) { v = min; }
            if (!isNaN(max) && v > max) { v = max; }

            var o = old;
            if (isNaN(v)) v = old;
            else old = v;

            if (o != v && raiseEvent) config.changed(v);
            var str = formatFloat(v, 8);
            self.value = str;
        }

        function step(dir) {
            var step = parseFloat(self.step);
            if (!step) step = 1.0;
            setValue(parseFloat(self.value) + dir * step, true);
        }

        function checkKey(e) {
            if ((e.key < "0" || e.key > "9") && e.key != "." && e.key != "+" && e.key != "-" && e.key != "E" && e.key != "e" && e.key != "*" && e.key != "/" && e.key != "(" && e.key != ")" && e.key != " ") e.preventDefault();

        }

        function down(e) {
            if (e.keyCode == 38) { step(1); e.preventDefault(); }
            else if (e.keyCode == 40) { step(-1); e.preventDefault(); }
            else if (e.keyCode == 33) { step(largeStep()); e.preventDefault(); }
            else if (e.keyCode == 34) { step(-largeStep()); e.preventDefault(); }
            else if (e.keyCode == 13) setValue(e.target.value, true);
            else if (e.keyCode == 27) setValue(old, true);
        }

        this["numericsetvalue"] = setValue;

        $(this)
            .bind('mousewheel', function (e) { step(-Math.sign(e.originalEvent.deltaY) * (e.originalEvent.shiftKey ? largeStep() : 1)); e.preventDefault(); })
            .keypress(function (e) { checkKey(e); })
            .keydown(function (e) { down(e); })
            .change(function (e) { setValue(e.target.value, true); });
    });
};
