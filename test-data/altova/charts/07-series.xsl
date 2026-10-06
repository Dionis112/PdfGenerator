<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
  exclude-result-prefixes="xs a">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/chart">
    <xsl:variable name="config" select="a:create-chart-config(string(@type), string(@title))"/>
    <xsl:variable name="series" select="a:create-chart-data-series(string(@series), point/@label/string(), point/@value/xs:double(.))"/>
    <xsl:variable name="chart" select="a:create-chart($config, $series)"/>
    <result>
      <image encoding="base64" format="default"><xsl:value-of select="a:generate-chart-image($chart, string(@width), string(@height), 'binarytobase64')"/></image>
      <image encoding="base64" format="png"><xsl:value-of select="a:generate-chart-image($chart, string(@width), string(@height), 'base64Binary', 'png')"/></image>
      <image encoding="hex" format="png"><xsl:value-of select="a:generate-chart-image($chart, string(@width), string(@height), 'hexBinary', 'png')"/></image>
    </result>
  </xsl:template>
</xsl:stylesheet>
