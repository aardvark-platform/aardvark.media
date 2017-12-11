function D3TestFunktion(input, id)
{
	
var data = input;
var series = d3.stack()
    .keys(["apples", "bananas", "cherries", "dates"])
    .offset(d3.stackOffsetDiverging)
    (data);

var svg = d3.select(id),
    margin = {top: 20, right: 30, bottom: 30, left: 60},
    width = +svg.attr("width"),
    height = +svg.attr("height");

var x = d3.scaleBand()
    .domain(data.map(function(d) { return d.month; }))
    .rangeRound([margin.left, width - margin.right])
    .padding(0.1);

var y = d3.scaleLinear()
    .domain([d3.min(series, stackMin), d3.max(series, stackMax)])
    .rangeRound([height - margin.bottom, margin.top]);

var z = d3.scaleOrdinal(d3.schemeCategory10);

svg.append("g")
  .selectAll("g")
  .data(series)
  .enter().append("g")
    .attr("fill", function(d) { return z(d.key); })
  .selectAll("rect")
  .data(function(d) { return d; })
  .enter().append("rect")
    .attr("width", x.bandwidth)
    .attr("x", function(d) { return x(d.data.month); })
    .attr("y", function(d) { return y(d[1]); })
    .attr("height", function(d) { return y(d[0]) - y(d[1]); })

svg.append("g")
    .attr("transform", "translate(0," + y(0) + ")")
    .call(d3.axisBottom(x));

svg.append("g")
    .attr("transform", "translate(" + margin.left + ",0)")
    .call(d3.axisLeft(y));

function stackMin(serie) {
  return d3.min(serie, function(d) { return d[0]; });
}

function stackMax(serie) {
  return d3.max(serie, function(d) { return d[1]; });
}
				
}

function HiliteAxis(id, minValue, maxValue, tickCount) {
    var host = d3.select(id);

    var svg = host.append("svg").attr("preserveAspectRatio", "xMinYMin meet").attr("viewBox", "0 0 500 500").classed("svg-content", true);

    // var svg = d3.select(id),
    // width = +svg.attr("width"),
    // height = +svg.attr("height");

    var margin = {
        top: 50,
        right: 20,
        left: 10,
        bottom: 10
    };

    //var innerWidth = width - margin.left - margin.right;
    //var innerHeight = height - margin.top - margin.bottom;

    var xScale = d3.scaleLinear().domain([minValue, maxValue]).range([margin.left, 500 - margin.right]);

    var ticks = xScale.ticks(tickCount)

    if (ticks.indexOf(maxValue) < 0) {
        ticks.push(maxValue)
    }

    if (ticks.indexOf(minValue) < 0) {
        ticks.unshift(minValue);
    }

    var xAxisB = d3.axisBottom(xScale).tickSizeInner(5).tickSizeOuter(15).tickValues(ticks)


    var ticks2 = xScale.ticks(tickCount * 2)

    if (ticks2.indexOf(maxValue) < 0) {
        ticks2.push(maxValue)
    }

    if (ticks2.indexOf(minValue) < 0) {
        ticks2.unshift(minValue);
    }

    var xAxisT = d3.axisTop(xScale).tickSize(20).tickValues(ticks2)


    svg.append("g")
        .attr('transform', 'translate(0,' + margin.top + ')')
        .classed('x axis', true)
        .call(xAxisT).selectAll("text").remove();

    svg.append('g')
        .attr('transform', 'translate(0,' + margin.top + ')')
        .classed('x axis', true)
        .call(xAxisB);

    // CREATE CUSTOM SUB-SELECTIONS
    d3.selection.prototype.first = function () {
        return d3.select(this._groups[0][0]);
    };

    d3.selection.prototype.last = function () {
        var last = this.size() - 1;
        return d3.select(this._groups[0][last]);
    };

    // SHIFT THE END LABELS
    var tickLabels = svg.selectAll('.axis.x .tick text');

    tickLabels.first().attr('transform', 'translate(0,10)');
    tickLabels.last().attr('transform', 'translate(0,10)');

    //(d3.selectAll("text")._groups[0][9]).attr('transform', 'translate(0,10)');
}