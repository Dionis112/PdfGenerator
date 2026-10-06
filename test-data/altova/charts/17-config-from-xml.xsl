<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
 xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
 xmlns:xbrli="http://www.xbrl.org/2003/instance" xmlns:t="urn:altova-fixture:taxonomy"
 exclude-result-prefixes="xs a xbrli t">
 <xsl:output method="xml" encoding="utf-8" indent="yes"/>
 <xsl:template match="/chart">
  <xsl:variable name="config" select="a:create-chart-config-from-xml(chart-config)"/>
  <xsl:variable name="series" select="a:create-chart-data-series('Выручка', point/@label/string(), point/@value/xs:double(.))"/>
  <xsl:variable name="chart" select="a:create-chart($config, $series)"/>
  <result><image encoding="base64" format="png"><xsl:value-of select="a:generate-chart-image($chart, string(@width), string(@height), 'base64Binary', 'png')"/></image></result>
 </xsl:template>
</xsl:stylesheet>
