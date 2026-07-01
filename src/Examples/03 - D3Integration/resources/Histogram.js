const margin = { top: 20, right: 30, bottom: 30, left: 30 };
const width = 960 - margin.left - margin.right;
const height = 500 - margin.top - margin.bottom;

function refresh(values, color) {
    if (values.length === 0) return;
    const rgb = d3.rgb(color);
    const formatCount = d3.format(",.0f");

    const max = d3.max(values);
    const min = d3.min(values);

    const x = d3.scaleLinear()
        .domain([min, max])
        .range([0, width]);

    const data = d3.bin()
        .domain(x.domain())
        .thresholds(x.ticks(20))
        (values);

    const yMax = d3.max(data, d => d.length);
    const yMin = d3.min(data, d => d.length);

    const colorScale = d3.scaleLinear()
        .domain([yMin, yMax])
        .range([rgb.brighter(), rgb.darker()]);

    const y = d3.scaleLinear()
        .domain([0, yMax])
        .range([height, 0]);

    const xAxis = d3.axisBottom(x).ticks(width / 80).tickSizeOuter(0);
    const yAxis = d3.axisLeft(y);

    const svg = d3.select("body")
        .selectAll("svg")
        .data([null])
        .join("svg")
        .attr("width", width + margin.left + margin.right)
        .attr("height", height + margin.top + margin.bottom)
        .selectAll("g.canvas-group")
        .data([null])
        .join("g")
        .attr("class", "canvas-group")
        .attr("transform", `translate(${margin.left}, ${margin.top})`);

    const bar = svg.selectAll(".bar")
        .data(data)
        .join(
            enter => {
                const g = enter.append("g")
                    .attr("class", "bar")
                    .attr("transform", d => `translate(${x(d.x0)}, ${height})`);

                g.append("rect")
                    .attr("x", 1)
                    .attr("width", d => Math.max(0, x(d.x1) - x(d.x0) - 1))
                    .attr("height", 0)
                    .attr("fill", rgb.brighter());

                g.append("text")
                    .attr("dy", ".75em")
                    .attr("text-anchor", "middle")
                    .attr("y", -12)
                    .attr("x", d => (x(d.x1) - x(d.x0)) / 2)
                    .text(0);

                return g;
            }
        );

    const t = svg.transition().duration(1000);

    bar.transition(t)
        .style("transform", d => `translate(${x(d.x0)}px, ${y(d.length)}px)`);

    bar.select("rect")
        .transition(t)
        .attr("width", d => Math.max(0, x(d.x1) - x(d.x0) - 1))
        .attr("height", d => height - y(d.length))
        .attr("fill", d => colorScale(d.length));

    bar.select("text")
        .transition(t)
        .attr("y", -12)
        .attr("x", d => (x(d.x1) - x(d.x0)) / 2)
        .text(d => formatCount(d.length));

    svg.selectAll(".x.axis")
        .data([null])
        .join("g")
        .attr("class", "x axis")
        .attr("transform", `translate(0, ${height})`)
        .transition(t)
        .call(xAxis);

    svg.selectAll(".y.axis")
        .data([null])
        .join("g")
        .attr("class", "y axis")
        .transition(t)
        .call(yAxis);
}

function setColor(color) {
    const svg = d3.select("body").select(".canvas-group");
    const bars = svg.selectAll(".bar");
    const data = bars.data();

    if (!data.length) return;

    const yMax = d3.max(data, d => d.length);
    const yMin = d3.min(data, d => d.length);

    const colorScale = d3.scaleLinear()
        .domain([yMin, yMax])
        .range([d3.rgb(color).brighter(), d3.rgb(color).darker()]);

    bars.select("rect")
        .transition()
        .duration(500)
        .attr("fill", d => colorScale(d.length));
}