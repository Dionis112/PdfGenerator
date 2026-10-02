<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:fo="http://www.w3.org/1999/XSL/Format">
  <xsl:template name="layout">
    <fo:layout-master-set>
      <fo:simple-page-master master-name="a4" page-width="210mm" page-height="297mm" margin="20mm">
        <fo:region-body/>
      </fo:simple-page-master>
    </fo:layout-master-set>
  </xsl:template>
</xsl:stylesheet>
