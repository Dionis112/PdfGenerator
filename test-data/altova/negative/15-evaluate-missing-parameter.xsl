<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:a="http://www.altova.com/xslt-extensions"
  exclude-result-prefixes="xs a">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/probe">
    <!-- This case must fail inside evaluate on an engine that supports it. -->
    <unexpected-success><xsl:value-of select="a:evaluate(string(@expression))"/></unexpected-success>
  </xsl:template>
</xsl:stylesheet>
