<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
  exclude-result-prefixes="xs a">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/chart">
    <xsl:variable name="config" select="a:create-chart-config('BarChart', string(@title))"/>
    <xsl:variable name="rows" select="for $row in row return a:create-chart-data-row(string($row/@label), xs:double($row/@plan), xs:double($row/@actual))"/>
    <xsl:variable name="series" select="a:create-chart-data-series-from-rows(series/@name/string(), $rows)"/>
    <xsl:variable name="chart" select="a:create-chart($config, $series)"/>
    <result><image encoding="base64" format="png"><xsl:value-of select="a:generate-chart-image($chart, string(@width), string(@height), 'base64Binary', 'png')"/></image></result>
  </xsl:template>
</xsl:stylesheet>
